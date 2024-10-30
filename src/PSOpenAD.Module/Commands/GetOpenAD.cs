using PSOpenAD.LDAP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace PSOpenAD.Module.Commands;

internal delegate OpenADEntity CreateADObjectDelegate(Dictionary<string, (object[], bool)> attributes);

public abstract class GetOpenADOperation<T> : OpenADSessionCmdletBase
    where T : ADObjectIdentity
{
    internal static StringComparer _caseInsensitiveComparer = StringComparer.OrdinalIgnoreCase;

    internal bool _includeDeleted = false;

    internal abstract AttributeDescriptor[] DefaultProperties { get; }

    internal abstract LDAPFilter FilteredClass { get; }

    internal abstract OpenADObject CreateADObject(Dictionary<string, (object[], bool)> attributes);

    #region Connection Parameters

    [Parameter(
        Mandatory = true,
        ParameterSetName = "SessionIdentity"
    )]
    [Parameter(
        Mandatory = true,
        ParameterSetName = "SessionLDAPFilter"
    )]
    public override OpenADSession? Session { get; set; }

    [Parameter(ParameterSetName = "ServerIdentity")]
    [Parameter(ParameterSetName = "ServerLDAPFilter")]
    [ArgumentCompleter(typeof(ServerCompleter))]
    public override string Server { get; set; } = "";

    [Parameter(ParameterSetName = "ServerIdentity")]
    [Parameter(ParameterSetName = "ServerLDAPFilter")]
    public override AuthenticationMethod AuthType { get; set; } = AuthenticationMethod.Default;

    [Parameter(ParameterSetName = "ServerIdentity")]
    [Parameter(ParameterSetName = "ServerLDAPFilter")]
    public override OpenADSessionOptions SessionOption { get; set; } = new OpenADSessionOptions();

    [Parameter(ParameterSetName = "ServerIdentity")]
    [Parameter(ParameterSetName = "ServerLDAPFilter")]
    public override SwitchParameter StartTLS { get; set; }

    [Parameter(ParameterSetName = "ServerIdentity")]
    [Parameter(ParameterSetName = "ServerLDAPFilter")]
    [Credential()]
    public override PSCredential? Credential { get; set; }

    #endregion

    #region LDAPFilter Parameters

    [Parameter(
        ValueFromPipelineByPropertyName = true,
        ParameterSetName = "ServerLDAPFilter"
    )]
    [Parameter(
        ValueFromPipelineByPropertyName = true,
        ParameterSetName = "SessionLDAPFilter"
    )]
    public string? LDAPFilter { get; set; }

    [Parameter(ParameterSetName = "ServerLDAPFilter")]
    [Parameter(ParameterSetName = "SessionLDAPFilter")]
    public string? SearchBase { get; set; }

    [Parameter(ParameterSetName = "ServerLDAPFilter")]
    [Parameter(ParameterSetName = "SessionLDAPFilter")]
    public SearchScope SearchScope { get; set; } = SearchScope.Subtree;

    #endregion

    #region Identity Parameters

    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        ParameterSetName = "ServerIdentity"
    )]
    [Parameter(
        Mandatory = true,
        Position = 0,
        ValueFromPipeline = true,
        ValueFromPipelineByPropertyName = true,
        ParameterSetName = "SessionIdentity"
    )]
    public T? Identity { get; set; }

    #endregion

    #region Common Parameters

    [Parameter()]
    [Alias("Properties")]
    [ValidateNotNullOrEmpty]
    [ArgumentCompleter(typeof(PropertyCompleter))]
    public string[]? Property { get; set; }

    #endregion

    protected override void ProcessRecordWithSession(OpenADSession session)
    {
        LDAPFilter finalFilter;
        if (!string.IsNullOrWhiteSpace(LDAPFilter))
        {
            LDAPFilter subFilter;
            try
            {
                subFilter = LDAP.LDAPFilter.ParseFilter(LDAPFilter);
            }
            catch (InvalidLDAPFilterException e)
            {
                ErrorRecord rec = new(
                    e,
                    "InvalidLDAPFilterException",
                    ErrorCategory.ParserError,
                    LDAPFilter);

                rec.ErrorDetails = new($"Failed to parse LDAP Filter: {e.Message}");

                // By setting the InvocationInfo we get a nice error description in PowerShell with positional
                // details. Unfortunately this is not publicly settable so we have to use reflection.
                if (!string.IsNullOrWhiteSpace(e.Filter))
                {
                    ScriptPosition start = new("", 1, e.StartPosition + 1, e.Filter);
                    ScriptPosition end = new("", 1, e.EndPosition + 1, e.Filter);
                    InvocationInfo info = InvocationInfo.Create(
                        MyInvocation.MyCommand,
                        new ScriptExtent(start, end));
                    rec.GetType().GetField(
                        "_invocationInfo",
                        BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(rec, info);
                }

                ThrowTerminatingError(rec);
                return; // Satisfies nullability checks
            }

            finalFilter = new FilterAnd(new[] { FilteredClass, subFilter });
        }
        else if (Identity != null)
        {
            finalFilter = new FilterAnd(new[] { FilteredClass, Identity.LDAPFilter });
        }
        else
        {
            finalFilter = FilteredClass;
        }

        List<LDAPControl>? serverControls = null;
        if (_includeDeleted)
        {
            serverControls = new()
            {
                new ShowDeleted(false),
                new ShowDeactivatedLink(false),
            };
        }

        string className = PropertyCompleter.GetClassNameForCommand(MyInvocation.MyCommand.Name);
        HashSet<string> requestedProperties = DefaultProperties
            .Select(p => p.Name)
            .ToHashSet(_caseInsensitiveComparer);
        string[] explicitProperties = Property ?? Array.Empty<string>();
        bool showAll = false;

        // We can only validate modules if there was metadata. Metadata may not be present on all systems and
        // when unauthenticated authentication was used.
        HashSet<string> validProperties = session.SchemaMetadata.GetClassAttributesInformation(className)
            ?? explicitProperties.ToHashSet(_caseInsensitiveComparer);

        HashSet<string> invalidProperties = new();

        foreach (string prop in explicitProperties)
        {
            if (prop == "*")
            {
                showAll = true;
                requestedProperties.Add(prop);
                continue;
            }

            if (validProperties.Contains(prop))
            {
                requestedProperties.Add(prop);
            }
            else
            {
                invalidProperties.Add(prop);
            }
        }

        if (invalidProperties.Count > 0)
        {
            string sortedProps = string.Join("', '", invalidProperties.OrderBy(p => p).ToArray());
            ErrorRecord rec = new(
                new ArgumentException($"One or more properties for {className} are not valid: '{sortedProps}'"),
                "InvalidPropertySet",
                ErrorCategory.InvalidArgument,
                null);

            ThrowTerminatingError(rec);
            return;
        }

        string searchBase = SearchBase ?? session.DefaultNamingContext;
        bool outputResult = false;

        HashSet<string> finalObjectProperties = requestedProperties
            .Where(v =>
                v != "*" &&
                (showAll || explicitProperties.Contains(v, _caseInsensitiveComparer)))
            .ToHashSet();

        foreach (SearchResultEntry result in SearchRequest(session, searchBase, finalFilter,
            requestedProperties.ToArray(), serverControls))
        {
            OpenADEntity adObj = CreateOutputObject(
                session,
                result,
                finalObjectProperties,
                CreateADObject,
                Logger
            );
            ProcessOutputObject(PSObject.AsPSObject(adObj));
            outputResult = true;
            WriteObject(adObj);
        }

        if (ParameterSetName.EndsWith("Identity") && !outputResult)
        {
            string msg = $"Cannot find an object with identity filter: '{finalFilter}' under: '{searchBase}'";
            ErrorRecord rec = new(new ItemNotFoundException(msg), "IdentityNotFound",
                ErrorCategory.ObjectNotFound, finalFilter.ToString());
            WriteError(rec);
        }
    }

    internal virtual IEnumerable<SearchResultEntry> SearchRequest(
        OpenADSession session,
        string searchBase,
        LDAPFilter filter,
        string[] attributes,
        IList<LDAPControl>? serverControls
    )
    {
        return Operations.LdapSearchRequest(session.Connection, searchBase, SearchScope, 0, session.OperationTimeout,
            filter, attributes, serverControls, CancelToken, Logger, false);
    }

    internal virtual void ProcessOutputObject(PSObject obj) { }

    /// <summary>
    /// Common code to create the ADObject output object.
    /// </summary>
    /// <param name="session">The OpenADSession the result is from.</param>
    /// <param name="result">The LDAP search result to use as the value source.</param>
    /// <param name="requestedProperties">All properties that should on the output object.</param>
    /// <param name="createFunc">If set is called to create the OpenADObject, otherwise OpenADObject is used.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>The created ADObject</returns>
    internal static OpenADEntity CreateOutputObject(
        OpenADSession session,
        SearchResultEntry result,
        HashSet<string>? requestedProperties,
        CreateADObjectDelegate? createFunc,
        ILogger logger
    )
    {
        Dictionary<string, (object[], bool)> props = new(_caseInsensitiveComparer);
        foreach (PartialAttribute attribute in result.Attributes)
        {
            props[attribute.Name] = session.SchemaMetadata.TransformAttributeValue(
                attribute.Name,
                attribute.Values,
                logger
            );
        }

        OpenADEntity adObj = createFunc == null ? new OpenADObject(props) : createFunc(props);
        AttributeDescriptor[] defaultProperties = (AttributeDescriptor[])adObj
            .GetType()
            .GetField("DEFAULT_PROPERTIES", BindingFlags.NonPublic | BindingFlags.Static)
            ?.GetValue(null)!;

        // This adds a note property for each explicitly requested property, excluding the ones the object
        // naturally exposes. Also adds the DomainController property to denote what DC the response came from.
        // Adds a note property for the DomainController that the response came from.
        PSObject adPSObj = PSObject.AsPSObject(adObj);
        adPSObj.Properties.Add(new PSNoteProperty("DomainController", session.DomainController));

        // Adds a note property for all the attributes in the result as well as all the properties requested. If the
        // requested property doesn't have a value, then null is used.
        IEnumerable<string> orderedProps;
        if (requestedProperties != null)
        {
            orderedProps = requestedProperties.Union(props.Keys, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            orderedProps = props.Keys;
        }

        foreach (string p in orderedProps.Where(v => !defaultProperties.Contains(new AttributeDescriptor(v, true))))
        {
            object? value = null;
            if (props.ContainsKey(p))
            {
                (value, bool isSingleValue) = props[p];
                if (isSingleValue)
                {
                    value = ((IList<object>)value)[0];
                }
            }

            // To make the properties more PowerShell like make sure the first char is in upper case.
            // PowerShell is case insensitive so users can still select it based on the lower case LDAP name.
            string propertyName = p[0..1].ToUpperInvariant() + p[1..];
            adPSObj.Properties.Add(new PSNoteProperty(propertyName, value));
        }

        return adObj;
    }
}

[Cmdlet(
    VerbsCommon.Get, "OpenADObject",
    DefaultParameterSetName = "ServerLDAPFilter"
)]
[OutputType(typeof(OpenADObject))]
public class GetOpenADObject : GetOpenADOperation<ADObjectIdentity>
{
    [Parameter()]
    public SwitchParameter IncludeDeletedObjects { get => _includeDeleted; set => _includeDeleted = value; }

    internal override AttributeDescriptor[] DefaultProperties => OpenADObject.DEFAULT_PROPERTIES;

    internal override LDAPFilter FilteredClass => new FilterPresent("objectClass");

    internal override OpenADObject CreateADObject(Dictionary<string, (object[], bool)> attributes)
        => new(attributes);
}

[Cmdlet(
    VerbsCommon.Get, "OpenADComputer",
    DefaultParameterSetName = "ServerLDAPFilter"
)]
[OutputType(typeof(OpenADComputer))]
public class GetOpenADComputer : GetOpenADOperation<ADPrincipalIdentityWithDollar>
{
    internal override AttributeDescriptor[] DefaultProperties => OpenADComputer.DEFAULT_PROPERTIES;

    internal override LDAPFilter FilteredClass
        => new FilterEquality("objectCategory", LDAP.LDAPFilter.EncodeSimpleFilterValue("computer"));

    internal override OpenADComputer CreateADObject(Dictionary<string, (object[], bool)> attributes)
        => new(attributes);
}

[Cmdlet(
    VerbsCommon.Get, "OpenADUser",
    DefaultParameterSetName = "ServerLDAPFilter"
)]
[OutputType(typeof(OpenADUser))]
public class GetOpenADUser : GetOpenADOperation<ADPrincipalIdentity>
{
    internal override AttributeDescriptor[] DefaultProperties => OpenADUser.DEFAULT_PROPERTIES;

    internal override LDAPFilter FilteredClass
        => new FilterAnd(new LDAPFilter[] {
            new FilterEquality("objectCategory", LDAP.LDAPFilter.EncodeSimpleFilterValue("person")),
            new FilterEquality("objectClass", LDAP.LDAPFilter.EncodeSimpleFilterValue("user"))
        });

    internal override OpenADUser CreateADObject(Dictionary<string, (object[], bool)> attributes)
        => new(attributes);
}

[Cmdlet(
    VerbsCommon.Get, "OpenADGroup",
    DefaultParameterSetName = "ServerLDAPFilter"
)]
[OutputType(typeof(OpenADGroup))]
public class GetOpenADGroup : GetOpenADOperation<ADPrincipalIdentity>
{
    internal override AttributeDescriptor[] DefaultProperties => OpenADGroup.DEFAULT_PROPERTIES;

    internal override LDAPFilter FilteredClass
        => new FilterEquality("objectCategory", LDAP.LDAPFilter.EncodeSimpleFilterValue("group"));

    internal override OpenADGroup CreateADObject(Dictionary<string, (object[], bool)> attributes)
        => new(attributes);
}

[Cmdlet(
    VerbsCommon.Get, "OpenADServiceAccount",
    DefaultParameterSetName = "ServerLDAPFilter"
)]
[OutputType(typeof(OpenADServiceAccount))]
public class GetOpenADServiceAccount : GetOpenADOperation<ADPrincipalIdentityWithDollar>
{
    internal override AttributeDescriptor[] DefaultProperties => OpenADServiceAccount.DEFAULT_PROPERTIES;

    internal override LDAPFilter FilteredClass
        => new FilterEquality("objectCategory", LDAP.LDAPFilter.EncodeSimpleFilterValue("msDS-GroupManagedServiceAccount"));

    internal override OpenADServiceAccount CreateADObject(Dictionary<string, (object[], bool)> attributes)
        => new(attributes);
}

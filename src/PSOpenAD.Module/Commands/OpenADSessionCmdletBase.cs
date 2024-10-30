using PSOpenAD.LDAP;
using System;
using System.Linq;
using System.Management.Automation;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace PSOpenAD.Module.Commands;

public abstract class OpenADCancellableCmdlet : PSCmdlet, IDisposable
{
    private bool _disposed = false;

    private CancellationTokenSource _cancelTokenSource = new();

    protected OpenADCancellableCmdlet()
    {
        Logger = new CmdletLogger(this);
    }

    protected CancellationToken CancelToken
    {
        get => _cancelTokenSource.Token;
    }

    protected ILogger Logger { get; }

    protected override void StopProcessing()
    {
        _cancelTokenSource.Cancel();
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _cancelTokenSource.Dispose();
        }
        _disposed = true;
    }
}

public abstract class OpenADSessionCmdletBase : OpenADCancellableCmdlet
{
    internal const string DefaultSessionParameterSet = "Server";

    [Parameter(
        Mandatory = true,
        ParameterSetName = "Session"
    )]
    public virtual OpenADSession? Session { get; set; }

    [Parameter(ParameterSetName = DefaultSessionParameterSet)]
    [ArgumentCompleter(typeof(ServerCompleter))]
    public virtual string Server { get; set; } = "";

    [Parameter(ParameterSetName = DefaultSessionParameterSet)]
    public virtual AuthenticationMethod AuthType { get; set; } = AuthenticationMethod.Default;

    [Parameter(ParameterSetName = DefaultSessionParameterSet)]
    public virtual OpenADSessionOptions SessionOption { get; set; } = new OpenADSessionOptions();

    [Parameter(ParameterSetName = DefaultSessionParameterSet)]
    public virtual SwitchParameter StartTLS { get; set; }

    [Parameter(ParameterSetName = DefaultSessionParameterSet)]
    [Credential()]
    public virtual PSCredential? Credential { get; set; }

    protected override void ProcessRecord()
    {
        if (string.IsNullOrEmpty(Server))
        {
            if (GlobalState.DefaultDC == null)
            {
                string msg = "Cannot determine default realm for implicit domain controller.";
                if (!string.IsNullOrEmpty(GlobalState.DefaultDCError))
                {
                    msg += $" {GlobalState.DefaultDCError}";
                }
                WriteError(new ErrorRecord(
                    new ArgumentException(msg),
                    "NoImplicitDomainController",
                    ErrorCategory.InvalidArgument,
                    null));
                return;
            }

            Server = GlobalState.DefaultDC.ToString();
        }

        OpenADSession? session = Session ?? OpenADSessionFactory.CreateOrUseDefaultAsync(
            Server,
            Credential?.GetNetworkCredential(),
            AuthType,
            StartTLS,
            SessionOption,
            CancelToken,
            Logger,
            defaultSession: ldapUri => GlobalState.Sessions.Find(s => s.Uri == ldapUri)
        ).GetAwaiter().GetResult();

        // If null, it failed to create session - error records have already been written.
        if (session != null)
        {
            GlobalState.RegisterSession(session);
            ProcessRecordWithSession(session);
        }
    }

    protected abstract void ProcessRecordWithSession(OpenADSession session);

    internal string? GetIdentityDistinguishedName(
        ADObjectIdentity identity,
        OpenADSession session,
        string verb)
    {
        WriteVerbose($"Attempting to get distinguishedName for object with filter '{identity.LDAPFilter}'");

        SearchResultEntry? entryResult = Operations.LdapSearchRequestAsync(
            session.Connection,
            session.DefaultNamingContext,
            SearchScope.Subtree,
            0,
            session.OperationTimeout,
            identity.LDAPFilter,
            new[] { "distinguishedName" },
            null,
            CancelToken,
            Logger,
            false
        ).FirstOrDefaultAsync().GetAwaiter().GetResult();

        PartialAttribute? dnResult = entryResult?.Attributes
            .Where(a => string.Equals(a.Name, "distinguishedName", StringComparison.InvariantCultureIgnoreCase))
            .FirstOrDefault();
        if (dnResult == null)
        {
            ErrorRecord error = new(
                new ArgumentException($"Failed to find object to set using the filter '{identity.LDAPFilter}'"),
                $"CannotFind{verb}ObjectWithFilter",
                ErrorCategory.InvalidArgument,
                identity);
            WriteError(error);
            return null;
        }

        (object[] rawDn, bool _) = session.SchemaMetadata.TransformAttributeValue(
            dnResult.Name,
            dnResult.Values,
            Logger);
        return (string)rawDn[0];
    }
}

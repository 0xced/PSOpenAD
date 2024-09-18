using System;
using PSOpenAD.LDAP;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace PSOpenAD;

internal static class Operations
{
    /// <summary>Performs an LDAP add operation.</summary>
    /// <param name="connection">The LDAP connection to perform the add on.</param>
    /// <param name="entry">The entry DN to create the object at.</param>
    /// <param name="attributes">The attributes and their values to set on the new object.</param>
    /// <param name="controls">Custom controls to use with the request</param>
    /// <param name="cancelToken">Token to cancel any network IO waits</param>
    /// <param name="logger">The logger logger.</param>
    /// <returns>The AddResponse from the request.</returns>
    public static AddResponse LdapAddRequest(
        IADConnection connection,
        string entry,
        PartialAttribute[] attributes,
        IList<LDAPControl>? controls,
        CancellationToken cancelToken,
        ILogger logger
    )
    {
        logger.LogTrace("Starting LDAP add request for {Entry}", entry);

        int addId = connection.Session.Add(entry, attributes, controls: controls);
        AddResponse addRes = (AddResponse)connection.WaitForMessage(addId, cancelToken: cancelToken);
        connection.RemoveMessageQueue(addId);

        if (addRes.Result.ResultCode != LDAPResultCode.Success)
        {
            using (logger.BeginScope(new Dictionary<string, string> { ["ErrorId"] = "LDAPAddFailure", ["ErrorCategory"] = "InvalidOperation" }))
            {
                logger.LogError("Failed to add {Entry}", addRes.Result);
            }
        }

        return addRes;
    }

    /// <summary>Performs an LDAP delete operation.</summary>
    /// <param name="connection">The LDAP connection to perform the delete on.</param>
    /// <param name="entry">The entry DN to delete.</param>
    /// <param name="controls">Custom controls to use with the request</param>
    /// <param name="cancelToken">Token to cancel any network IO waits</param>
    /// <param name="logger">The PSCmdlet that is running the operation.</param>
    /// <returns>The DelResponse from the request.</returns>
    public static DelResponse LdapDeleteRequest(
        IADConnection connection,
        string entry,
        IList<LDAPControl>? controls,
        CancellationToken cancelToken,
        ILogger logger
    )
    {
        logger.LogTrace("Starting LDAP delete request for {Entry}", entry);

        int addId = connection.Session.Delete(entry, controls: controls);
        DelResponse delRes = (DelResponse)connection.WaitForMessage(addId, cancelToken: cancelToken);
        connection.RemoveMessageQueue(addId);

        if (delRes.Result.ResultCode != LDAPResultCode.Success)
        {
            using (logger.BeginScope(new Dictionary<string, string> { ["ErrorId"] = "LDAPDeleteFailure", ["ErrorCategory"] = "InvalidOperation" }))
            {
                logger.LogError("Failed to delete {Entry}", delRes.Result);
            }
        }

        return delRes;
    }

    /// <summary>Performs an LDAP modify operation.</summary>
    /// <param name="connection">The LDAP connection to perform the modify on.</param>
    /// <param name="entry">The entry DN to modify.</param>
    /// <param name="changes">The changes to perform on the object.</param>
    /// <param name="controls">Custom controls to use with the request</param>
    /// <param name="cancelToken">Token to cancel any network IO waits</param>
    /// <param name="logger">The logger logger.</param>
    /// <returns>The ModifyResponse from the request.</returns>
    public static ModifyResponse LdapModifyRequest(
        IADConnection connection,
        string entry,
        ModifyChange[] changes,
        IList<LDAPControl>? controls,
        CancellationToken cancelToken,
        ILogger logger
    )
    {
        logger.LogTrace("Starting LDAP modify request for {Entry}", entry);

        int addId = connection.Session.Modify(entry, changes, controls: controls);
        ModifyResponse modifyRes = (ModifyResponse)connection.WaitForMessage(addId, cancelToken: cancelToken);
        connection.RemoveMessageQueue(addId);

        if (modifyRes.Result.ResultCode != LDAPResultCode.Success)
        {
            using (logger.BeginScope(new Dictionary<string, string> { ["ErrorId"] = "LDAPModifyFailure", ["ErrorCategory"] = "InvalidOperation" }))
            {
                logger.LogError("Failed to modify {Entry}", modifyRes.Result);
            }
        }

        return modifyRes;
    }

    /// <summary>Performs an LDAP modify DN operation.</summary>
    /// <param name="connection">The LDAP connection to perform the modify on.</param>
    /// <param name="entry">The entry DN to manage.</param>
    /// <param name="newRDN">The new RDN of the entry</param>
    /// <param name="deleteOldRDN">Delete the old RDN attribute</param>
    /// <param name="newSuperior">If not null or whitespace, the new object to move the entry to.</param>
    /// <param name="controls">Custom controls to use with the request</param>
    /// <param name="cancelToken">Token to cancel any network IO waits</param>
    /// <param name="logger">The logger logger.</param>
    /// <returns>The ModifyDNResponse from the request.</returns>
    public static ModifyDNResponse LdapModifyDNRequest(
        IADConnection connection,
        string entry,
        string newRDN,
        bool deleteOldRDN,
        string? newSuperior,
        IList<LDAPControl>? controls,
        CancellationToken cancelToken,
        ILogger logger
    )
    {
        string targetDN = string.IsNullOrWhiteSpace(newSuperior) ? string.Empty : $",{newSuperior}";
        logger.LogTrace("Starting LDAP modify DN request for {Entry}->{NewRDN}{TargetDN}", entry, newRDN, targetDN);

        int addId = connection.Session.ModifyDN(
            entry,
            newRDN,
            deleteOldRDN,
            newSuperior: newSuperior,
            controls: controls);
        ModifyDNResponse modifyRes = (ModifyDNResponse)connection.WaitForMessage(addId, cancelToken: cancelToken);
        connection.RemoveMessageQueue(addId);

        if (modifyRes.Result.ResultCode != LDAPResultCode.Success)
        {
            using (logger.BeginScope(new Dictionary<string, string> { ["ErrorId"] = "LDAPModifyFailure", ["ErrorCategory"] = "InvalidOperation" }))
            {
                logger.LogError("Failed to modify DN {Entry}->{NewRDN}{TargetDN}", modifyRes.Result, newRDN, targetDN);
            }
        }

        return modifyRes;
    }

    /// <summary>Performs an LDAP search operation.</summary>
    /// <param name="connection">The LDAP connection to perform the search on.</param>
    /// <param name="searchBase">The search base of the query.</param>
    /// <param name="scope">The scope of the query.</param>
    /// <param name="sizeLimit"></param>
    /// <param name="timeLimit"></param>
    /// <param name="filter">The LDAP filter to use for the query.</param>
    /// <param name="attributes">The attributes to retrieve.</param>
    /// <param name="cancelToken">Token to cancel any network IO waits</param>
    /// <param name="logger">The logger logger.</param>
    /// <param name="ignoreErrors">Ignore errors and do not write to the error stream.</param>
    /// <returns>Yields each returned result containing the attributes requested from the search request.</returns>
    public static IEnumerable<SearchResultEntry> LdapSearchRequest(
        IADConnection connection,
        string searchBase,
        SearchScope scope,
        int sizeLimit,
        int timeLimit,
        LDAPFilter filter,
        string[] attributes,
        IList<LDAPControl>? controls,
        CancellationToken cancelToken,
        ILogger logger,
        bool ignoreErrors
    )
    {
        logger.LogTrace("Starting LDAP search request at '{SearchBase}' for {Scope} - {Filter}", searchBase, scope, filter);

        int searchId = 0;
        int paginationLimit = sizeLimit > 0 ? sizeLimit : 1000;
        byte[]? paginationCookie = null;
        bool request = true;

        while (true)
        {
            if (request)
            {
                List<LDAPControl> copiedControls = controls?.ToList() ?? new();
                copiedControls.Add(new PagedResultControl(false, paginationLimit, paginationCookie));
                searchId = connection.Session.Search(searchBase, scope, DereferencingPolicy.Never, sizeLimit,
                    timeLimit / 1000, false, filter, attributes, copiedControls);

                request = false;
            }

            LDAPMessage searchRes = connection.WaitForMessage(searchId, cancelToken: cancelToken);
            if (searchRes is SearchResultDone resultDone)
            {
                PagedResultControl? paginateControl = resultDone.Controls?.OfType<PagedResultControl>().FirstOrDefault();
                if (resultDone.Result.ResultCode == LDAPResultCode.Success && paginateControl?.Cookie?.Length > 0)
                {
                    logger.LogTrace("Receive pagination result, sending new search request");
                    request = true;
                    paginationCookie = paginateControl.Cookie;

                    continue;
                }
                else if (resultDone.Result.ResultCode == LDAPResultCode.SizeLimitExceeded)
                {
                    logger.LogWarning("Exceeded size limit of search request - results may be incomplete.");
                }
                else if (!ignoreErrors && resultDone.Result.ResultCode == LDAPResultCode.Referral)
                {
                    var state = new Dictionary<string, string>
                    {
                        ["ErrorId"] = "LDAPReferral",
                        ["ErrorCategory"] = "ResourceUnavailable",
                        ["ErrorDetails"] = $"A referral was returned from the server that points to: '{string.Join("', '", resultDone.Result.Referrals ?? Array.Empty<string>())}'",
                        ["ErrorRecommendedAction"] = "Perform request on one of the referral URIs",
                    };
                    using (logger.BeginScope(state))
                    {
                        logger.LogError("{Message}", resultDone.Result);
                    }
                }
                else if (!ignoreErrors && resultDone.Result.ResultCode != LDAPResultCode.Success)
                {
                    using (logger.BeginScope(new Dictionary<string, string> { ["ErrorId"] = "LDAPSearchFailure", ["ErrorCategory"] = "InvalidOperation" }))
                    {
                        logger.LogError("{Message}", resultDone.Result);
                    }
                }
                break;
            }
            else if (searchRes is SearchResultReference)
            {
                continue;
            }

            yield return (SearchResultEntry)searchRes;
        }
        connection.RemoveMessageQueue(searchId);
    }
}

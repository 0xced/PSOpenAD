using DnsClient;
using PSOpenAD.Native;
using System;
using System.ComponentModel;
using System.Linq;
using System.Management.Automation;
using System.Runtime.InteropServices;

namespace PSOpenAD.Module;

public class OnModuleImportAndRemove : IModuleAssemblyInitializer, IModuleAssemblyCleanup
{
    public void OnImport()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            const GetDcFlags getDcFlags = GetDcFlags.DS_IS_DNS_NAME | GetDcFlags.DS_ONLY_LDAP_NEEDED |
                GetDcFlags.DS_RETURN_DNS_NAME | GetDcFlags.DS_WRITABLE_REQUIRED;
            string? dcName = null;
            try
            {
                DCInfo dcInfo = NetApi32.DsGetDcName(null, null, null, getDcFlags, null);
                dcName = dcInfo.Name?.TrimStart('\\');
            }
            catch (Win32Exception e) when (e.NativeErrorCode == 1355) // ERROR_NO_SUCH_DOMAIN
            {
                // While it's questionable why you would use this module if it hasn't been joined to a domain it's
                // still possible to use this for any LDAP server on Windows so just ignore the default DC setup.
            }
            catch (Exception e)
            {
                GlobalState.DefaultDCError = $"Failure calling DsGetDcName to get default DC: {e.Message}";
            }

            if (!string.IsNullOrWhiteSpace(dcName))
            {
                GlobalState.DefaultDC = new($"ldap://{dcName}:389/");
            }
            else if (string.IsNullOrEmpty(GlobalState.DefaultDCError))
            {
                GlobalState.DefaultDCError = "No configured default DC on host";
            }
        }
        else
        {
            if (!GSSAPI.Providers[AuthenticationMethod.Kerberos].Available)
            {
                GlobalState.DefaultDCError = "Failed to find GSSAPI library";
            }
            else
            {
                // If the krb5 API is available, attempt to get the default realm used when creating an implicit
                // session.
                try
                {
                    string defaultRealm = "";
                    using SafeKrb5Context ctx = Kerberos.InitContext();
                    try
                    {
                        defaultRealm = Kerberos.GetDefaultRealm(ctx);
                    }
                    catch (KerberosException e)
                    {
                        GlobalState.DefaultDCError = $"Failed to lookup krb5 default_realm: {e.Message}";
                    }

                    if (!string.IsNullOrWhiteSpace(defaultRealm))
                    {
                        // _ldap._tcp.dc._msdcs.domain.com
                        string baseDomain = $"dc._msdcs.{defaultRealm}";
                        LookupClient dnsLookup = new();
                        try
                        {
                            ServiceHostEntry[] res = dnsLookup.ResolveService(baseDomain, "ldap",
                                System.Net.Sockets.ProtocolType.Tcp);

                            ServiceHostEntry? first = res.OrderBy(r => r.Priority).ThenBy(r => r.Weight).FirstOrDefault();
                            if (first != null)
                            {
                                GlobalState.DefaultDC = new($"ldap://{first.HostName}:{first.Port}/");
                            }
                            else
                            {
                                GlobalState.DefaultDCError = $"No SRV records for _ldap._tcp.{baseDomain} found";
                            }
                        }
                        catch (DnsResponseException e)
                        {
                            GlobalState.DefaultDCError = $"DNS Error looking up SRV records for _ldap._tcp.{baseDomain}: {e.Message}";
                        }
                        catch (Exception e)
                        {
                            GlobalState.DefaultDCError = $"Unknown error looking up SRV records for _ldap._tcp.{baseDomain}: {e.GetType().Name} - {e.Message}";
                        }
                    }
                }
                catch (DllNotFoundException)
                {
                    GlobalState.DefaultDCError = "Failed to find Kerberos library";
                }
            }
        }
    }

    public void OnRemove(PSModuleInfo module)
    {
        foreach (OpenADSession session in GlobalState.Sessions)
            session.Close();

        GlobalState.Sessions = new();
        // TODO: Resolver?.Dispose();
    }
}

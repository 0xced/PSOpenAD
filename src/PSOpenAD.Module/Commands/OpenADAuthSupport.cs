using System.Management.Automation;
using PSOpenAD.Native;

namespace PSOpenAD.Module.Commands;

[Cmdlet(
    VerbsCommon.Get, "OpenADAuthSupport"
)]
[OutputType(typeof(AuthenticationProvider))]
public class GetOpenADAuthSupport : PSCmdlet
{
    protected override void EndProcessing()
    {
        foreach (AuthenticationProvider provider in GSSAPI.Providers.Values)
            WriteObject(provider);
    }
}

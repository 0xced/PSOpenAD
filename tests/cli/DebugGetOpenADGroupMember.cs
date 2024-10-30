using System.Management.Automation;
using System.Reflection;
using PSOpenAD;
using PSOpenAD.Module.Commands;

public class DebugGetOpenADGroupMember : GetOpenADGroupMember, IDebugCommand
{
    private readonly DebugRuntime _runtime = new();

    public DebugGetOpenADGroupMember()
    {
        CommandRuntime = _runtime;
        GetType().GetProperty("CommandInfo", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(this, new CmdletInfo("Get-OpenADGroupMember", GetType()));
    }

    public IEnumerable<OpenADEntity> Run()
    {
        BeginProcessing();
        ProcessRecord();
        EndProcessing();
        return _runtime.Results.OfType<OpenADPrincipal>();
    }
}
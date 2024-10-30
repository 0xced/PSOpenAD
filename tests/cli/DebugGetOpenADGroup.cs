using System.Management.Automation;
using System.Reflection;
using PSOpenAD;
using PSOpenAD.Module.Commands;

public class DebugGetOpenADGroup : GetOpenADGroup, IDebugCommand
{
    private readonly DebugRuntime _runtime = new();

    public DebugGetOpenADGroup()
    {
        CommandRuntime = _runtime;
        GetType().GetProperty("CommandInfo", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(this, new CmdletInfo("Get-OpenADGroup", GetType()));
    }

    public IEnumerable<OpenADEntity> Run()
    {
        BeginProcessing();
        ProcessRecord();
        EndProcessing();
        return _runtime.Results.OfType<OpenADGroup>();
    }
}
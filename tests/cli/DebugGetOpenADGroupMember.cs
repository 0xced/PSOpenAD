using PSOpenAD;
using PSOpenAD.Module.Commands;

public class DebugGetOpenADGroupMember : GetOpenADGroupMember, IDebugCommand
{
    private readonly DebugRuntime _runtime = new();

    public DebugGetOpenADGroupMember() => CommandRuntime = _runtime;

    public IEnumerable<OpenADEntity> Run()
    {
        BeginProcessing();
        ProcessRecord();
        EndProcessing();
        return _runtime.Results.OfType<OpenADPrincipal>();
    }
}
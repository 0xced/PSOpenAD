using PSOpenAD;
using PSOpenAD.Module.Commands;

public class DebugGetOpenADGroup : GetOpenADGroup, IDebugCommand
{
    private readonly DebugRuntime _runtime = new();

    public DebugGetOpenADGroup() => CommandRuntime = _runtime;

    public IEnumerable<OpenADEntity> Run()
    {
        BeginProcessing();
        ProcessRecord();
        EndProcessing();
        return _runtime.Results.OfType<OpenADGroup>();
    }
}
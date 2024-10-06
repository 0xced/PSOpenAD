using PSOpenAD;
using PSOpenAD.Module.Commands;

public class DebugGetOpenADRootDSE : GetOpenADRootDSE, IDebugCommand
{
    private readonly DebugRuntime _runtime = new();

    public DebugGetOpenADRootDSE() => CommandRuntime = _runtime;

    public IEnumerable<OpenADEntity> Run()
    {
        BeginProcessing();
        ProcessRecord();
        EndProcessing();
        return _runtime.Results.OfType<OpenADEntity>();
    }
}
using PSOpenAD;
using PSOpenAD.Module.Commands;

public class DebugGetOpenADUser : GetOpenADUser, IDebugCommand
{
    private readonly DebugRuntime _runtime = new();

    public DebugGetOpenADUser() => CommandRuntime = _runtime;

    public IEnumerable<OpenADEntity> Run()
    {
        BeginProcessing();
        ProcessRecord();
        EndProcessing();
        return _runtime.Results.OfType<OpenADEntity>();
    }
}
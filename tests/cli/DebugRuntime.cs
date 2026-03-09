using System.Diagnostics.CodeAnalysis;
using System.Management.Automation;
using System.Management.Automation.Host;

public class DebugRuntime : ICommandRuntime
{
    private readonly List<object?> _results = new();
    public IReadOnlyCollection<object?> Results => _results;
    public void WriteObject(object? sendToPipeline) => _results.Add(sendToPipeline);
    public void WriteDebug(string text) => Console.WriteLine($"🐛 {text}");
    public void WriteError(ErrorRecord errorRecord) => Console.WriteLine($"❌ {errorRecord}");
    public void WriteObject(object? sendToPipeline, bool enumerateCollection) => throw new NotImplementedException();
    public void WriteProgress(ProgressRecord progressRecord) => throw new NotImplementedException();
    public void WriteProgress(long sourceId, ProgressRecord progressRecord) => throw new NotImplementedException();
    public void WriteVerbose(string text) => Console.WriteLine($"📝 {text}");
    public void WriteWarning(string text) => Console.WriteLine($"⚠️ {text}");
    public void WriteCommandDetail(string text) => throw new NotImplementedException();
    public bool ShouldProcess(string? target) => throw new NotImplementedException();
    public bool ShouldProcess(string? target, string? action) => throw new NotImplementedException();
    public bool ShouldProcess(string? verboseDescription, string? verboseWarning, string? caption) => throw new NotImplementedException();
    public bool ShouldProcess(string? verboseDescription, string? verboseWarning, string? caption, out ShouldProcessReason shouldProcessReason) => throw new NotImplementedException();
    public bool ShouldContinue(string? query, string? caption) => throw new NotImplementedException();
    public bool ShouldContinue(string? query, string? caption, ref bool yesToAll, ref bool noToAll) => throw new NotImplementedException();
    public bool TransactionAvailable() => throw new NotImplementedException();
    [DoesNotReturn]
    public void ThrowTerminatingError(ErrorRecord errorRecord) => throw errorRecord.Exception;
    public PSHost? Host => null;
    public PSTransactionContext? CurrentPSTransaction => null;
}
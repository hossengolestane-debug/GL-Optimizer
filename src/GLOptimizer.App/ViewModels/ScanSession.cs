namespace GLOptimizer.App.ViewModels;

/// <summary>
/// Cancels the previous scan when a new one starts. Callers ignore results whose generation is no longer current.
/// </summary>
public sealed class ScanSession
{
    private CancellationTokenSource? _cts;
    private int _generation;

    public (int Generation, CancellationToken Token) Start()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var generation = Interlocked.Increment(ref _generation);
        return (generation, _cts.Token);
    }

    public void Cancel() => _cts?.Cancel();

    public bool IsCurrent(int generation) => generation == Volatile.Read(ref _generation);
}

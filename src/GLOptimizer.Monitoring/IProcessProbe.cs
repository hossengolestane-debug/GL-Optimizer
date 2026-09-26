namespace GLOptimizer.Monitoring;

public interface IProcessProbe
{
    ProcessProbeResult Capture();
}

public sealed class ProcessProbeResult
{
    public bool Available { get; init; }

    public bool HadUnreadableMatch { get; init; }

    public IReadOnlyList<ProbedProcess> Processes { get; init; } = [];
}

public sealed class ProbedProcess
{
    public int ProcessId { get; init; }

    public string ProcessName { get; init; } = string.Empty;

    public string? ExecutablePath { get; init; }

    public TimeSpan? TotalProcessorTime { get; init; }

    public long? WorkingSetBytes { get; init; }
}

using GLOptimizer.Core.Models;

namespace GLOptimizer.Core.Monitoring;

public sealed class SystemReading
{
    public double? CpuPercent { get; init; }

    public double? GpuPercent { get; init; }

    public long? GpuVramUsedBytes { get; init; }

    public double? RamPercent { get; init; }

    public long? RamUsedBytes { get; init; }

    public long? RamTotalBytes { get; init; }

    public double? DiskPercent { get; init; }
}

public sealed class GameLoopReading
{
    public double? CpuPercent { get; init; }

    public long? RamBytes { get; init; }

    public GameRunStatus State { get; init; } = GameRunStatus.Unknown;
}

public sealed class MetricSample
{
    public DateTimeOffset Timestamp { get; init; }

    public double? CpuPercent { get; init; }

    public double? GpuPercent { get; init; }

    public long? GpuVramUsedBytes { get; init; }

    public double? RamPercent { get; init; }

    public long? RamUsedBytes { get; init; }

    public long? RamTotalBytes { get; init; }

    public double? DiskPercent { get; init; }

    public double? GameLoopCpuPercent { get; init; }

    public long? GameLoopRamBytes { get; init; }

    public GameRunStatus GameLoopState { get; init; } = GameRunStatus.Unknown;
}

namespace GLOptimizer.Core.Models;

/// <summary>
/// Populated only by a real hardware provider. Phase 0 does not fill this type.
/// </summary>
public sealed class HardwareReport
{
    public string? CpuName { get; init; }

    public string? GpuName { get; init; }

    public long? TotalMemoryBytes { get; init; }
}

/// <summary>
/// Populated only by a real frame provider. Phase 0 does not fill this type.
/// </summary>
public sealed class FrameSample
{
    public double? FramesPerSecond { get; init; }

    public double? FrameTimeMilliseconds { get; init; }

    public DateTimeOffset? CapturedAt { get; init; }
}

/// <summary>
/// Populated only after a real, read-only detector exists. Phase 0 does not search for GameLoop.
/// </summary>
public sealed class GameLoopInstallation
{
    public string? InstallPath { get; init; }

    public string? Version { get; init; }
}

public sealed class AppMarketStatus
{
    public string? Summary { get; init; }
}

public sealed class BackupRecord
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public long SizeBytes { get; init; }
}

public sealed class OptimizationAction
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }
}

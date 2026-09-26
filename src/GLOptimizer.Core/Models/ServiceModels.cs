namespace GLOptimizer.Core.Models;

/// <summary>
/// Values a hardware probe actually returned. Null fields were not reported.
/// </summary>
public sealed class HardwareReport
{
    public string? CpuName { get; init; }

    public int? PhysicalCores { get; init; }

    public int? LogicalCores { get; init; }

    public string? GpuName { get; init; }

    public long? GpuMemoryBytes { get; init; }

    public long? TotalMemoryBytes { get; init; }

    public string? StorageType { get; init; }

    public int? MonitorRefreshHz { get; init; }

    public string? WindowsVersion { get; init; }

    public string? WindowsBuild { get; init; }

    public string? Architecture { get; init; }

    public bool? VirtualizationFirmwareEnabled { get; init; }

    public bool? HypervisorPresent { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Populated only by a real frame provider. Frame capture is not part of detection.
/// </summary>
public sealed class FrameSample
{
    public double? FramesPerSecond { get; init; }

    public double? FrameTimeMilliseconds { get; init; }

    public DateTimeOffset? CapturedAt { get; init; }
}

/// <summary>
/// One verified GameLoop install. Version and engine stay null when they were not read.
/// </summary>
public sealed class GameLoopInstallation
{
    public string? InstallPath { get; init; }

    public string? Version { get; init; }

    public string? Engine { get; init; }

    public GameRunStatus RunStatus { get; init; }

    public string? LauncherPath { get; init; }

    /// <summary>
    /// GameLoopData directory reported by the product registry. It is not an install root.
    /// </summary>
    public string? DataPath { get; init; }

    public IReadOnlyList<GameLoopProcessInfo> Processes { get; init; } = [];
}

/// <summary>
/// Read-only result of a GameLoop scan. Empty installations means the search finished and found nothing.
/// </summary>
public sealed class GameLoopScan
{
    public IReadOnlyList<GameLoopInstallation> Installations { get; init; } = [];

    public MobileGamePresence PubgMobile { get; init; } = new();

    public MobileGamePresence CodMobile { get; init; } = new();

    /// <summary>
    /// True when an uninstall entry names GameLoop but no verified install directory was found.
    /// </summary>
    public bool BrokenRegistration { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>
    /// Every registry key, path, and process name the scan tried, including misses.
    /// </summary>
    public IReadOnlyList<ScanCheck> Checks { get; init; } = [];

    /// <summary>
    /// Existing GameLoopData directories. Package search does not walk these trees.
    /// </summary>
    public IReadOnlyList<string> DataRoots { get; init; } = [];

    /// <summary>
    /// Parent folders that contain App Market data, such as MobileGamePC.
    /// </summary>
    public IReadOnlyList<string> MarketRoots { get; init; } = [];
}

public sealed class ScanCheck
{
    public required string Kind { get; init; }

    public required string Target { get; init; }

    public bool Found { get; init; }

    public string? Detail { get; init; }
}

public sealed class GameLoopProcessInfo
{
    public int ProcessId { get; init; }

    public required string ProcessName { get; init; }

    public required string ExecutablePath { get; init; }
}

public sealed class MobileGamePresence
{
    public GamePresenceStatus Status { get; init; }

    public string? Version { get; init; }

    public string? PackageId { get; init; }

    public string? Path { get; init; }

    public string? Detail { get; init; }
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

    public bool Damaged { get; init; }

    public string? Problem { get; init; }
}

public sealed class OptimizationAction
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }
}

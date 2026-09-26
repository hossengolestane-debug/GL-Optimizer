namespace GLOptimizer.Core.Optimization;

public enum OptimizationProfile
{
    Performance = 0,
    Balanced = 1,
    Quality = 2,
    Custom = 3
}

public enum HardwareTier
{
    Unknown = 0,
    Low = 1,
    Mid = 2,
    High = 3
}

public enum RecommendationStatus
{
    Applicable = 0,
    AlreadyOptimal = 1,
    Skipped = 2,
    NotSupported = 3,
    NotImplemented = 4
}

public enum RiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

public sealed class RawSettingKey
{
    public required string Name { get; init; }

    public required string Raw { get; init; }
}

public sealed class LocatedSetting
{
    public required string Name { get; init; }

    public required string Path { get; init; }

    public required string Format { get; init; }

    public string? Normalized { get; init; }

    public bool FromRegistry { get; init; }

    public bool Valid { get; init; }

    public IReadOnlyList<RawSettingKey> Keys { get; init; } = [];
}

public sealed class OptimizationEdit
{
    public required string Setting { get; init; }

    public required string Path { get; init; }

    public required string Format { get; init; }

    public required string Key { get; init; }

    public required string CurrentRaw { get; init; }

    public required string NewRaw { get; init; }
}

public sealed class OptimizationRecommendation
{
    public required string Setting { get; init; }

    public string? CurrentValue { get; init; }

    public string? RecommendedValue { get; init; }

    public required string Reason { get; init; }

    public RiskLevel Risk { get; init; }

    public bool RequiresRestart { get; init; }

    public bool Reversible { get; init; }

    public RecommendationStatus Status { get; init; }

    public IReadOnlyList<OptimizationEdit> Edits { get; init; } = [];
}

public sealed class OptimizationAnalysis
{
    public OptimizationProfile Profile { get; init; }

    public HardwareTier Tier { get; init; }

    public bool GameLoopRunning { get; init; }

    public string? Notice { get; init; }

    public IReadOnlyList<OptimizationRecommendation> Recommendations { get; init; } = [];
}

public sealed class OptimizationPreview
{
    public required OptimizationAnalysis Analysis { get; init; }

    public IReadOnlyList<OptimizationEdit> Edits { get; init; } = [];

    public bool CanApply { get; init; }

    public string? BlockReason { get; init; }
}

public sealed class OptimizationReport
{
    public int Applied { get; init; }

    public int AlreadyOptimal { get; init; }

    public int Skipped { get; init; }

    public string? BackupId { get; init; }

    public bool RestartRequired { get; init; }

    public required string Summary { get; init; }

    public required string ResultText { get; init; }
}

public sealed class OptimizationUndoRecord
{
    public string BackupId { get; set; } = string.Empty;

    public string Profile { get; set; } = string.Empty;

    public DateTimeOffset AppliedAtUtc { get; set; }
}

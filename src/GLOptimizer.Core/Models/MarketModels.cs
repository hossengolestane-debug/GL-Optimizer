using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;

namespace GLOptimizer.Core.Models;

public sealed class MarketInventoryItem
{
    public required string Path { get; init; }

    public required string RelativePath { get; init; }

    public bool IsDirectory { get; init; }

    public long SizeBytes { get; init; }

    public int FileCount { get; init; }

    public DateTimeOffset? LastWriteTimeUtc { get; init; }

    public MarketItemKind Kind { get; init; }

    public MarketConfidence Confidence { get; init; }

    public required string Reason { get; init; }
}

public sealed class OfficialVersionResult
{
    public string? Version { get; init; }

    public string? Host { get; init; }

    public bool RequestedNetwork { get; init; }

    public string? Detail { get; init; }
}

public sealed class AppMarketReport
{
    public string StatusText { get; init; } = "UNKNOWN";

    public GamePresenceStatus InstalledStatus { get; init; }

    public string? InstalledVersion { get; init; }

    public string? MarketVersion { get; init; }

    public string? OfficialVersion { get; init; }

    public string? OfficialDetail { get; init; }

    public bool OfficialRequested { get; init; }

    public DateTimeOffset? LastScanUtc { get; init; }

    public CatalogComparison Comparison { get; init; }

    public string? Issue { get; init; }

    public string? PackagePath { get; init; }

    public IReadOnlyList<string> InstallPaths { get; init; } = [];

    public IReadOnlyList<MarketInventoryItem> Inventory { get; init; } = [];

    public IReadOnlyList<MarketInventoryItem> RepairTargets { get; init; } = [];

    public bool ScanTruncated { get; init; }

    public string? Detail { get; init; }
}

public sealed class CodMobileReport
{
    public GamePresenceStatus InstalledStatus { get; init; }

    public string? InstalledDetail { get; init; }

    public string? PackagePath { get; init; }

    public AppMarketReport Market { get; init; } = new();

    public IReadOnlyList<DiagnosticFinding> Findings { get; init; } = [];
}

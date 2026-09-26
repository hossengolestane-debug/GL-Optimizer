using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;

namespace GLOptimizer.Core.Repair;

public sealed class RepairCandidate
{
    public required string FullPath { get; init; }

    public required string RelativePath { get; init; }

    public MarketItemKind Kind { get; init; }

    public bool IsDirectory { get; init; }

    public long SizeBytes { get; init; }

    public int FileCount { get; init; }

    public bool IsReparse { get; init; }

    public bool UnderVerifiedRoot { get; init; }
}

public sealed class RepairAssessment
{
    public IReadOnlyList<RepairCandidate> Clear { get; init; } = [];

    public IReadOnlyList<RepairCandidate> Backup { get; init; } = [];

    public IReadOnlyList<string> Unexpected { get; init; } = [];

    public long ClearBytes { get; init; }

    public int ClearFiles { get; init; }

    public bool NeedsReview { get; init; }

    public string? ReviewReason { get; init; }

    public bool CanRepair => !NeedsReview && Clear.Count > 0;
}

/// <summary>
/// Decides which inventory entries a repair may move. Only cache under a verified root is eligible.
/// </summary>
public static class AppMarketRepairRules
{
    public const long MaxBytes = 5L * 1024 * 1024 * 1024;

    public const int MaxFiles = 20_000;

    public static RepairAssessment Assess(IReadOnlyList<RepairCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var clear = new List<RepairCandidate>();
        var backup = new List<RepairCandidate>();
        var unexpected = new List<string>();
        foreach (var candidate in candidates)
        {
            if (candidate.Kind == MarketItemKind.Metadata
                && !candidate.IsDirectory
                && candidate.UnderVerifiedRoot
                && !candidate.IsReparse
                && !IsProtected(candidate.RelativePath))
            {
                backup.Add(candidate);
            }

            if (candidate.Kind != MarketItemKind.Cache)
            {
                continue;
            }

            if (!candidate.UnderVerifiedRoot || candidate.IsReparse || IsProtected(candidate.RelativePath))
            {
                unexpected.Add(candidate.RelativePath);
                continue;
            }

            clear.Add(candidate);
        }

        long bytes = 0;
        var files = 0;
        foreach (var item in clear)
        {
            bytes += item.SizeBytes < 0 ? 0 : item.SizeBytes;
            files += item.FileCount < 0 ? 0 : item.FileCount;
        }

        string? reason = null;
        if (unexpected.Count > 0)
        {
            reason = "The cache set includes a path that is not safe to clear.";
        }
        else if (files > MaxFiles)
        {
            reason = "The cache set has more than " + MaxFiles.ToString(System.Globalization.CultureInfo.InvariantCulture) + " files.";
        }
        else if (bytes > MaxBytes)
        {
            reason = "The cache set is larger than 5 GB.";
        }

        return new RepairAssessment
        {
            Clear = clear,
            Backup = backup,
            Unexpected = unexpected,
            ClearBytes = bytes,
            ClearFiles = files,
            NeedsReview = reason is not null,
            ReviewReason = reason
        };
    }

    public static bool IsProtected(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return true;
        }

        var normalized = relativePath.Replace('\\', '/');
        var name = normalized[(normalized.LastIndexOf('/') + 1)..];
        if (name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".obb", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".xapk", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized.Contains("/keymap/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/keymaps/", StringComparison.OrdinalIgnoreCase)
            || name.Contains("keymap", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var packageId in MobilePackages.Cod.Concat(MobilePackages.Pubg))
        {
            if (name.Equals(packageId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

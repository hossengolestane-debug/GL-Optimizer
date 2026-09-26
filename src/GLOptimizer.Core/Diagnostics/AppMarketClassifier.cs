using GLOptimizer.Core.Detection;

namespace GLOptimizer.Core.Diagnostics;

public enum MarketItemKind
{
    Cache = 0,
    Metadata = 1,
    Package = 2,
    Unknown = 3
}

public enum MarketConfidence
{
    High = 0,
    Medium = 1,
    Low = 2
}

public sealed class MarketClassification
{
    public MarketItemKind Kind { get; init; }

    public MarketConfidence Confidence { get; init; }

    public required string Reason { get; init; }
}

/// <summary>
/// Classifies a path inside a verified GameLoop install. Names come from the known launcher and package lists.
/// The directory layout is an assumption until it is checked on a real install.
/// </summary>
public static class AppMarketClassifier
{
    public const string Assumption = "This layout is an assumption until verified on a real install.";

    public static MarketClassification? Classify(string? relativePath, bool isDirectory)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var normalized = relativePath.Replace('\\', '/').Trim('/');
        if (normalized.Length == 0 || normalized.StartsWith("../", StringComparison.Ordinal) || normalized.Contains("/../", StringComparison.Ordinal))
        {
            return null;
        }

        var name = NameOf(normalized);
        if (name.Length == 0)
        {
            return null;
        }

        if (!isDirectory && name.Equals("AppMarket.exe", StringComparison.OrdinalIgnoreCase))
        {
            return Item(
                MarketItemKind.Package,
                MarketConfidence.High,
                "File name is AppMarket.exe, a known GameLoop launcher name. " + Assumption);
        }

        if (isDirectory && IsCodPackage(name))
        {
            return Item(
                MarketItemKind.Package,
                MarketConfidence.High,
                "Directory name matches a known COD Mobile package id. " + Assumption);
        }

        if (!isDirectory && IsVersionFile(name) && ParentIsCodPackage(normalized))
        {
            return Item(
                MarketItemKind.Metadata,
                MarketConfidence.High,
                "Version file name sits inside a known COD Mobile package directory. " + Assumption);
        }

        var underAppMarket = HasSegment(normalized, "AppMarket");
        if (underAppMarket && isDirectory && IsCacheName(name))
        {
            return Item(
                MarketItemKind.Cache,
                MarketConfidence.Medium,
                "Path contains AppMarket and a cache segment. " + Assumption);
        }

        if (underAppMarket && !isDirectory && IsMetadataExtension(name) && !IsInsideCache(normalized))
        {
            return Item(
                MarketItemKind.Metadata,
                MarketConfidence.Medium,
                "File extension looks like metadata and the path contains AppMarket. " + Assumption);
        }

        if (IsInsideCache(normalized))
        {
            return null;
        }

        if (!isDirectory && ParentIsCodPackage(normalized))
        {
            return null;
        }

        if (underAppMarket)
        {
            return Item(
                MarketItemKind.Unknown,
                MarketConfidence.Low,
                "Name matched AppMarket but the role is unknown. " + Assumption);
        }

        return null;
    }

    private static MarketClassification Item(MarketItemKind kind, MarketConfidence confidence, string reason) => new()
    {
        Kind = kind,
        Confidence = confidence,
        Reason = reason
    };

    private static bool IsCodPackage(string name)
    {
        foreach (var packageId in MobilePackages.Cod)
        {
            if (name.Equals(packageId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ParentIsCodPackage(string normalized)
    {
        var slash = normalized.LastIndexOf('/');
        if (slash <= 0)
        {
            return false;
        }

        return IsCodPackage(NameOf(normalized[..slash]));
    }

    private static bool IsVersionFile(string name)
    {
        foreach (var candidate in GameLoopLayout.VersionFileNames)
        {
            if (name.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMetadataExtension(string name)
    {
        return name.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".ini", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCacheName(string name) =>
        name.Equals("cache", StringComparison.OrdinalIgnoreCase)
        || name.Equals("caches", StringComparison.OrdinalIgnoreCase);

    private static bool IsInsideCache(string normalized)
    {
        var slash = normalized.LastIndexOf('/');
        if (slash <= 0)
        {
            return false;
        }

        var parent = normalized[..slash];
        return IsCacheName(NameOf(parent)) || HasSegment(parent, "cache") || HasSegment(parent, "caches");
    }

    private static bool HasSegment(string normalized, string segment)
    {
        var parts = normalized.Split('/');
        foreach (var part in parts)
        {
            if (part.Equals(segment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NameOf(string normalized)
    {
        var slash = normalized.LastIndexOf('/');
        return slash < 0 ? normalized : normalized[(slash + 1)..];
    }
}

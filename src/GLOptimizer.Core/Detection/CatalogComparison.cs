namespace GLOptimizer.Core.Detection;

public enum CatalogComparison
{
    Unknown = 0,
    Match = 1,
    VersionMismatch = 2,
    LocalMarketOutdated = 3,
    RemoteCatalogIssue = 4
}

public sealed class CatalogComparisonResult
{
    public CatalogComparison Comparison { get; init; }

    public required string Detail { get; init; }
}

/// <summary>
/// Compares installed, local market, and official versions. Remote catalog issues are not concluded here.
/// </summary>
public static class CatalogComparisonLogic
{
    public const string RemoteMessage =
        "Server-side GameLoop catalog issue detected. This cannot safely be modified locally.";

    public const string CannotDistinguish = "Cannot yet distinguish local vs remote.";

    public static string Badge(CatalogComparison comparison) => comparison switch
    {
        CatalogComparison.Match => "MATCH",
        CatalogComparison.VersionMismatch => "VERSION MISMATCH",
        CatalogComparison.LocalMarketOutdated => "LOCAL MARKET OUTDATED",
        CatalogComparison.RemoteCatalogIssue => "REMOTE CATALOG ISSUE",
        _ => "UNKNOWN"
    };

    public static CatalogComparisonResult Evaluate(string? installedRaw, string? marketRaw, string? officialRaw, string productName = "COD Mobile")
    {
        if (HasText(installedRaw) && PackageVersion.Parse(installedRaw) is null)
        {
            return Unknown("Installed version is not an unambiguous version string.");
        }

        if (HasText(marketRaw) && PackageVersion.Parse(marketRaw) is null)
        {
            return Unknown("Market version is not an unambiguous version string.");
        }

        var installed = PackageVersion.Parse(installedRaw);
        var market = PackageVersion.Parse(marketRaw);
        if (installed is null || market is null)
        {
            return Unknown("A local version is missing.");
        }

        if (installed.CompareTo(market) != 0)
        {
            return new CatalogComparisonResult
            {
                Comparison = CatalogComparison.VersionMismatch,
                Detail = productName + " version mismatch: installed " + installed.Text + ", market " + market.Text
            };
        }

        if (!HasText(officialRaw))
        {
            return new CatalogComparisonResult
            {
                Comparison = CatalogComparison.Match,
                Detail = "Installed and market versions match."
            };
        }

        var official = PackageVersion.Parse(officialRaw);
        if (official is null)
        {
            return Unknown("Official version is not an unambiguous version string.");
        }

        var marketVersusOfficial = market.CompareTo(official);
        if (marketVersusOfficial == 0)
        {
            return new CatalogComparisonResult
            {
                Comparison = CatalogComparison.Match,
                Detail = "Installed, market, and official versions match."
            };
        }

        if (marketVersusOfficial < 0)
        {
            return new CatalogComparisonResult
            {
                Comparison = CatalogComparison.LocalMarketOutdated,
                Detail = "Local market version " + market.Text + " is older than official version " + official.Text + "."
            };
        }

        return Unknown(CannotDistinguish);
    }

    private static CatalogComparisonResult Unknown(string detail) => new()
    {
        Comparison = CatalogComparison.Unknown,
        Detail = detail
    };

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);
}

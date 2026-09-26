using GLOptimizer.Core.Detection;

namespace GLOptimizer.Core.Repair;

public enum RepairVerdictKind
{
    Failed = 0,
    AwaitingRefresh = 1,
    LocalRefreshed = 2,
    RemoteCatalogIssue = 3,
    Unknown = 4
}

public sealed class RepairVerdict
{
    public RepairVerdictKind Kind { get; init; }

    public CatalogComparison Comparison { get; init; }

    public required string Message { get; init; }
}

/// <summary>
/// Compares the market version saved before a repair with the version read after GameLoop has been reopened.
/// </summary>
public static class RepairVerdictLogic
{
    public const string RemoteBody =
        "The local App Market has been refreshed successfully. However, the currently detected GameLoop server catalog still provides the same COD Mobile version. GL Optimizer cannot safely change GameLoop's remote catalog.";

    public const string AwaitingMessage =
        "Re-check after GameLoop refresh. The pre-repair market version is kept for that comparison.";

    public const string FailedMessage =
        "The repair did not finish. No remote catalog change is reported.";

    public static string LocalMessage(string version) =>
        "Local App Market refreshed. COD Mobile market version is now " + version + ".";

    public static RepairVerdict Evaluate(
        bool repairSucceeded,
        bool refreshObserved,
        string? preMarket,
        string? postMarket,
        string? installed)
    {
        if (!repairSucceeded)
        {
            return new RepairVerdict
            {
                Kind = RepairVerdictKind.Failed,
                Comparison = CatalogComparison.Unknown,
                Message = FailedMessage
            };
        }

        if (!refreshObserved)
        {
            return new RepairVerdict
            {
                Kind = RepairVerdictKind.AwaitingRefresh,
                Comparison = CatalogComparison.Unknown,
                Message = AwaitingMessage
            };
        }

        var pre = PackageVersion.Parse(preMarket);
        var post = PackageVersion.Parse(postMarket);
        var installedVersion = PackageVersion.Parse(installed);
        if (post is null)
        {
            return new RepairVerdict
            {
                Kind = RepairVerdictKind.Unknown,
                Comparison = CatalogComparison.Unknown,
                Message = "The market version after refresh is not unambiguous. No remote update is reported."
            };
        }

        var advanced = pre is not null && post.CompareTo(pre) > 0;
        var matchesInstalled = installedVersion is not null && post.CompareTo(installedVersion) == 0;
        if (advanced || matchesInstalled)
        {
            return new RepairVerdict
            {
                Kind = RepairVerdictKind.LocalRefreshed,
                Comparison = CatalogComparison.Match,
                Message = LocalMessage(post.Text)
            };
        }

        var same = pre is not null && post.CompareTo(pre) == 0;
        var outdated = installedVersion is null || post.CompareTo(installedVersion) < 0;
        if (same && outdated)
        {
            return new RepairVerdict
            {
                Kind = RepairVerdictKind.RemoteCatalogIssue,
                Comparison = CatalogComparison.RemoteCatalogIssue,
                Message = RemoteBody + " " + CatalogComparisonLogic.RemoteMessage
            };
        }

        return new RepairVerdict
        {
            Kind = RepairVerdictKind.Unknown,
            Comparison = CatalogComparison.Unknown,
            Message = CatalogComparisonLogic.CannotDistinguish
        };
    }
}

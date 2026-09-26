using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.GameLoop;

public sealed class AppMarketDiagnostics : IAppMarketDiagnostics
{
    private readonly IGameLoopDetector _detector;
    private readonly AppMarketDetector _inventory;
    private readonly AppMarketVersionService _versions;
    private readonly AppMarketCacheManager _caches;
    private readonly CodMobileVersionChecker _checker;
    private readonly IOfficialVersionSource _official;
    private readonly ILogStore _log;
    private readonly IClock _clock;

    public AppMarketDiagnostics(
        IGameLoopDetector detector,
        AppMarketDetector inventory,
        AppMarketVersionService versions,
        AppMarketCacheManager caches,
        CodMobileVersionChecker checker,
        IOfficialVersionSource official,
        ILogStore log,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(caches);
        ArgumentNullException.ThrowIfNull(checker);
        ArgumentNullException.ThrowIfNull(official);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(clock);
        _detector = detector;
        _inventory = inventory;
        _versions = versions;
        _caches = caches;
        _checker = checker;
        _official = official;
        _log = log;
        _clock = clock;
    }

    public async Task<OperationResult<AppMarketReport>> ScanAsync(bool checkOfficialVersion, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return OperationResult<AppMarketReport>.Failure("The scan was cancelled.");
        }

        try
        {
            var detected = await _detector.DetectAsync(cancellationToken).ConfigureAwait(false);
            if (!detected.Succeeded || detected.Value is null)
            {
                return OperationResult<AppMarketReport>.Failure(detected.Error ?? "GameLoop could not be scanned.");
            }

            OfficialVersionResult? official = null;
            if (checkOfficialVersion)
            {
                official = await _official.TryGetAsync(detected.Value.CodMobile.PackageId, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(official.Host))
                {
                    _log.Write(LogSeverity.Information, "Version", "Check Version contacted host " + official.Host + ".");
                }
                else
                {
                    _log.Write(LogSeverity.Information, "Version", "Check Version did not contact a host. " + (official.Detail ?? "No host was named."));
                }
            }

            return OperationResult<AppMarketReport>.Success(Build(detected.Value, official, checkOfficialVersion, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            return OperationResult<AppMarketReport>.Failure("The scan was cancelled.");
        }
        catch (Exception)
        {
            return OperationResult<AppMarketReport>.Failure("App Market could not be scanned.");
        }
    }

    private AppMarketReport Build(GameLoopScan scan, OfficialVersionResult? official, bool officialRequested, CancellationToken cancellationToken)
    {
        var items = new List<MarketInventoryItem>();
        var paths = new List<string>();
        var truncated = false;
        var marketVersions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var installation in scan.Installations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(installation.InstallPath))
            {
                continue;
            }

            var root = InstallPathRules.TryNormalize(installation.InstallPath);
            if (root is null)
            {
                continue;
            }

            paths.Add(root);
            var walked = _inventory.Scan(root, AppMarketDetector.DefaultMaxDepth, AppMarketDetector.DefaultMaxEntries, cancellationToken);
            if (walked.Truncated || !walked.Completed)
            {
                truncated = true;
            }

            items.AddRange(walked.Items);
            var market = _versions.ReadMarketVersion(root, walked.Items, cancellationToken);
            if (market is not null)
            {
                marketVersions.Add(market);
            }
        }

        var installed = ReadInstalled(scan, items);
        var marketVersion = marketVersions.Count == 1 ? marketVersions.First() : null;
        var officialVersion = PackageVersion.Parse(official?.Version)?.Text;
        var comparison = _checker.Check(installed, marketVersion, officialRequested ? officialVersion : null);
        var officialDetail = officialRequested
            ? official?.Detail ?? UnavailableOfficialVersionSource.NotImplementedDetail
            : "Not requested. Check Version is the only control that contacts a host.";

        return new AppMarketReport
        {
            StatusText = CatalogComparisonLogic.Badge(comparison.Comparison),
            InstalledStatus = scan.CodMobile.Status,
            InstalledVersion = installed,
            MarketVersion = marketVersion,
            OfficialVersion = officialRequested ? officialVersion : null,
            OfficialDetail = officialDetail,
            OfficialRequested = officialRequested,
            LastScanUtc = _clock.UtcNow,
            Comparison = comparison.Comparison,
            Issue = comparison.Detail,
            PackagePath = scan.CodMobile.Path,
            InstallPaths = paths,
            Inventory = items,
            RepairTargets = _caches.ListCaches(items),
            ScanTruncated = truncated,
            Detail = scan.Installations.Count == 0
                ? "GameLoop was not found, so App Market was not searched."
                : truncated
                    ? "The inventory stopped at the scan limit."
                    : scan.CodMobile.Detail
        };
    }

    private string? ReadInstalled(GameLoopScan scan, IReadOnlyList<MarketInventoryItem> items)
    {
        var directories = new List<string>();
        if (!string.IsNullOrWhiteSpace(scan.CodMobile.Path))
        {
            directories.Add(scan.CodMobile.Path);
        }

        foreach (var item in items)
        {
            if (!item.IsDirectory || item.Kind != MarketItemKind.Package)
            {
                continue;
            }

            var name = Path.GetFileName(item.RelativePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            foreach (var packageId in MobilePackages.Cod)
            {
                if (name.Equals(packageId, StringComparison.OrdinalIgnoreCase))
                {
                    directories.Add(item.Path);
                    break;
                }
            }
        }

        var found = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in directories)
        {
            if (!seen.Add(directory))
            {
                continue;
            }

            var version = _checker.ReadInstalledVersion(directory);
            if (version is not null)
            {
                found.Add(version);
            }
        }

        return found.Count == 1 ? found.First() : null;
    }
}

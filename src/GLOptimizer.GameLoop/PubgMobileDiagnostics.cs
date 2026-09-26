using System.Text;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Optimization;
using GLOptimizer.Core.Repair;
using GLOptimizer.Core.Results;

namespace GLOptimizer.GameLoop;

public sealed class PubgMobileReport
{
    public GamePresenceStatus InstalledStatus { get; init; }

    public string? InstalledDetail { get; init; }

    public string? PackagePath { get; init; }

    public string? InstalledVersion { get; init; }

    public string? MarketVersion { get; init; }

    public string? OfficialVersion { get; init; }

    public string? OfficialDetail { get; init; }

    public bool OfficialRequested { get; init; }

    public CatalogComparison Comparison { get; init; }

    public string StatusText { get; init; } = "UNKNOWN";

    public string? Issue { get; init; }

    public string LaunchStatus { get; init; } = "Unknown";

    public string CpuDetail { get; init; } = "Unknown";

    public string LogDetail { get; init; } = "No engine log line was found.";

    public string OptimizationProfile { get; init; } = "Balanced";

    public int ApplicableRecommendations { get; init; }

    public IReadOnlyList<DiagnosticFinding> Findings { get; init; } = [];
}

public interface IPubgMobileDiagnostics
{
    Task<OperationResult<PubgMobileReport>> RunAsync(CancellationToken cancellationToken = default);
}

public sealed class PubgMobileDiagnostics : IPubgMobileDiagnostics
{
    private readonly IGameLoopDetector _detector;
    private readonly IAppMarketDiagnostics _market;
    private readonly AppMarketVersionService _versions;
    private readonly CodMobileVersionChecker _versionsFiles;
    private readonly IProcessControl _processes;
    private readonly IWindowTitleSource _windows;
    private readonly IGameLoopMonitor _gameLoop;
    private readonly ISampleDelay _delay;
    private readonly IClock _clock;
    private readonly IOptimizationService _optimization;
    private readonly OptimizationProfileSelection _profile;

    private static readonly string[][] LogCandidates =
    [
        ["ui", "log.txt"],
        ["log", "aow_exe.log"],
        ["Engine", "log.txt"],
        ["AppMarket", "log.txt"]
    ];

    public PubgMobileDiagnostics(
        IGameLoopDetector detector,
        IAppMarketDiagnostics market,
        AppMarketVersionService versions,
        CodMobileVersionChecker versionFiles,
        IProcessControl processes,
        IWindowTitleSource windows,
        IGameLoopMonitor gameLoop,
        ISampleDelay delay,
        IClock clock,
        IOptimizationService optimization,
        OptimizationProfileSelection profile)
    {
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(market);
        ArgumentNullException.ThrowIfNull(versions);
        ArgumentNullException.ThrowIfNull(versionFiles);
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(gameLoop);
        ArgumentNullException.ThrowIfNull(delay);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(optimization);
        ArgumentNullException.ThrowIfNull(profile);
        _detector = detector;
        _market = market;
        _versions = versions;
        _versionsFiles = versionFiles;
        _processes = processes;
        _windows = windows;
        _gameLoop = gameLoop;
        _delay = delay;
        _clock = clock;
        _optimization = optimization;
        _profile = profile;
    }

    public async Task<OperationResult<PubgMobileReport>> RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var scan = await _detector.DetectAsync(cancellationToken).ConfigureAwait(false);
            if (!scan.Succeeded || scan.Value is null)
            {
                return OperationResult<PubgMobileReport>.Failure(scan.Error ?? "PUBG Mobile could not be scanned.");
            }

            var presence = scan.Value.PubgMobile;
            var roots = GameLoopLocations.InstallAndData(scan.Value);
            var installed = _versionsFiles.ReadInstalledVersion(presence.Path);
            string? marketVersion = null;
            var market = await _market.ScanAsync(checkOfficialVersion: false, cancellationToken).ConfigureAwait(false);
            if (market.Succeeded && market.Value is not null)
            {
                foreach (var root in market.Value.InstallPaths.Count > 0 ? market.Value.InstallPaths : roots)
                {
                    marketVersion = _versions.ReadMarketVersion(root, market.Value.Inventory, MobilePackages.Pubg, cancellationToken);
                    if (marketVersion is not null)
                    {
                        break;
                    }
                }
            }

            var comparison = CatalogComparisonLogic.Evaluate(installed, marketVersion, officialRaw: null, "PUBG Mobile");
            var processes = _processes.List();
            var launch = LaunchStatus(roots, processes);
            var cpu = await SampleCpuAsync(cancellationToken).ConfigureAwait(false);
            var logs = ReadLogs(roots);
            var analysis = await _optimization.AnalyzeAsync(_profile.Current, cancellationToken).ConfigureAwait(false);
            var applicable = analysis.Succeeded && analysis.Value is not null
                ? analysis.Value.Recommendations.Count(item => item.Status == RecommendationStatus.Applicable)
                : 0;
            var findings = new List<DiagnosticFinding>
            {
                Finding("PUBG Mobile install", presence.Detail ?? presence.Status.ToString(), "Read from the verified install.", presence.Status == GamePresenceStatus.Installed ? FindingOutcome.Pass : FindingOutcome.Unknown),
                Finding("Version comparison", comparison.Detail, "Official PUBG version stays Unknown. No host is contacted.", comparison.Comparison == CatalogComparison.Unknown ? FindingOutcome.Unknown : comparison.Comparison == CatalogComparison.Match ? FindingOutcome.Pass : FindingOutcome.Warning),
                Finding("Launch status", launch, "Process and window evidence under the verified install.", launch == "Running" ? FindingOutcome.Pass : FindingOutcome.Unknown),
                Finding("Engine CPU", cpu, "A short local sample. No process memory is read.", cpu == "Unknown" ? FindingOutcome.Unknown : FindingOutcome.Pass),
                Finding("Engine log", logs, "Local log lines already on disk. They are not uploaded.", logs.StartsWith("No engine", StringComparison.Ordinal) ? FindingOutcome.Unknown : FindingOutcome.Warning),
                Finding("Optimization profile", _profile.Current + " has " + applicable + " applicable recommendation(s).", "Uses the Phase 5 engine. No PUBG-specific settings are written.", FindingOutcome.Pass)
            };
            return OperationResult<PubgMobileReport>.Success(new PubgMobileReport
            {
                InstalledStatus = presence.Status,
                InstalledDetail = presence.Detail,
                PackagePath = presence.Path,
                InstalledVersion = installed,
                MarketVersion = marketVersion,
                OfficialVersion = null,
                OfficialDetail = "Official version source not implemented.",
                OfficialRequested = false,
                Comparison = comparison.Comparison,
                StatusText = CatalogComparisonLogic.Badge(comparison.Comparison),
                Issue = comparison.Detail,
                LaunchStatus = launch,
                CpuDetail = cpu,
                LogDetail = logs,
                OptimizationProfile = _profile.Current.ToString(),
                ApplicableRecommendations = applicable,
                Findings = findings
            });
        }
        catch (OperationCanceledException)
        {
            return OperationResult<PubgMobileReport>.Failure("The scan was cancelled.");
        }
        catch (Exception)
        {
            return OperationResult<PubgMobileReport>.Failure("PUBG Mobile diagnostics could not finish.");
        }
    }

    private static bool PubgProcessRunning(IReadOnlyList<string> roots, IReadOnlyList<ControlledProcess> processes)
    {
        foreach (var process in processes)
        {
            if (UnderInstall(process, roots) && IsPubgProcess(process))
            {
                return true;
            }
        }

        return false;
    }

    private string LaunchStatus(IReadOnlyList<string> roots, IReadOnlyList<ControlledProcess> processes)
    {
        if (PubgProcessRunning(roots, processes))
        {
            return "Running";
        }

        var windows = _windows.List();
        if (!windows.Succeeded || windows.Value is null)
        {
            return processes.Count == 0 ? "Unknown" : "Not running";
        }

        foreach (var window in windows.Value)
        {
            if (!window.Title.Contains("PUBG", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var process in processes)
            {
                if (process.ProcessId == window.ProcessId && UnderInstall(process, roots))
                {
                    return "Running";
                }
            }
        }

        return "Not running";
    }

    private async Task<string> SampleCpuAsync(CancellationToken cancellationToken)
    {
        try
        {
            var first = _gameLoop.Read(_clock.UtcNow);
            await _delay.WaitAsync(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            var second = _gameLoop.Read(_clock.UtcNow);
            if (first.CpuPercent is null || second.CpuPercent is null)
            {
                return "Unknown";
            }

            return second.CpuPercent.Value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "%";
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return "Unknown";
        }
    }

    private static string ReadLogs(IReadOnlyList<string> roots)
    {
        var lines = new List<string>();
        foreach (var root in roots)
        {
            foreach (var segments in LogCandidates)
            {
                var path = GameLoopLayout.Combine(root, segments);
                if (!File.Exists(path) || !InstallPathRules.IsUnderRoot(path, root) || IsReparse(path))
                {
                    continue;
                }

                foreach (var line in ReadInterestingLines(path))
                {
                    lines.Add(ReportRedaction.Redact(line, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
                    if (lines.Count == 8)
                    {
                        return string.Join(" ", lines);
                    }
                }
            }
        }

        return lines.Count == 0 ? "No engine log line was found." : string.Join(" ", lines);
    }

    private static IReadOnlyList<string> ReadInterestingLines(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var take = (int)Math.Min(stream.Length, 65536);
            if (take == 0)
            {
                return [];
            }

            stream.Seek(stream.Length - take, SeekOrigin.Begin);
            var buffer = new byte[take];
            stream.ReadExactly(buffer);
            var hits = new List<string>();
            foreach (var raw in Encoding.UTF8.GetString(buffer).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (!raw.Contains("error", StringComparison.OrdinalIgnoreCase)
                    && !raw.Contains("fail", StringComparison.OrdinalIgnoreCase)
                    && !raw.Contains("exception", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var line = raw.Trim();
                if (line.Length > 240)
                {
                    line = line[..240];
                }

                hits.Add(line);
                if (hits.Count == 8)
                {
                    break;
                }
            }

            return hits;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }

    private static bool IsPubgProcess(ControlledProcess process)
    {
        if (process.ProcessName.Contains("pubg", StringComparison.OrdinalIgnoreCase)
            || (process.ExecutablePath?.Contains("pubg", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return true;
        }

        foreach (var packageId in MobilePackages.Pubg)
        {
            if (process.ExecutablePath?.Contains(packageId, StringComparison.OrdinalIgnoreCase) ?? false)
            {
                return true;
            }
        }

        return false;
    }

    private static bool UnderInstall(ControlledProcess process, IReadOnlyList<string> roots)
    {
        if (string.IsNullOrWhiteSpace(process.ExecutablePath))
        {
            return false;
        }

        foreach (var root in roots)
        {
            if (InstallPathRules.IsUnderRoot(process.ExecutablePath, root))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsReparse(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return true;
        }
    }

    private static DiagnosticFinding Finding(string title, string evidence, string action, FindingOutcome outcome) => new()
    {
        Title = title,
        Evidence = evidence,
        RecommendedAction = action,
        Outcome = outcome
    };
}

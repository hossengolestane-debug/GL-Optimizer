using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Configuration;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Navigation;
using GLOptimizer.Core.Results;
using GLOptimizer.Infrastructure;

namespace GLOptimizer.App.ViewModels;

public partial class DiagnosticsViewModel : PageViewModel, IRefreshable
{
    private readonly AppDataLocations _locations;
    private readonly IHardwareService _hardware;
    private readonly IFrameMetricsProvider _frames;
    private readonly IGameLoopDetector _gameLoop;
    private readonly IGameLoopConfigDiscovery _config;
    private readonly ICodMobileDiagnostics _cod;
    private readonly IOptimizationService _optimization;
    private readonly IBackupService _backups;
    private readonly ScanSession _session = new();

    public DiagnosticsViewModel(
        AppDataLocations locations,
        IHardwareService hardware,
        IFrameMetricsProvider frames,
        IGameLoopDetector gameLoop,
        IGameLoopConfigDiscovery config,
        ICodMobileDiagnostics cod,
        IOptimizationService optimization,
        IBackupService backups)
        : base(AppPage.Diagnostics)
    {
        _locations = locations;
        _hardware = hardware;
        _frames = frames;
        _gameLoop = gameLoop;
        _config = config;
        _cod = cod;
        _optimization = optimization;
        _backups = backups;
    }

    public ObservableCollection<DiagnosticRowModel> Rows { get; } = new();

    public string SafetyNote => Phase0Notices.Safety + " " + Phase0Notices.ReadOnlyGameLoop;

    [RelayCommand]
    private Task RunChecksAsync() => RunScanAsync();

    [RelayCommand]
    private void CancelScan() => _session.Cancel();

    public void Refresh() => _ = RunScanAsync();

    private async Task RunScanAsync()
    {
        var (generation, token) = _session.Start();
        Rows.Clear();
        Rows.Add(PathRow("App data", _locations.Root, Directory.Exists(_locations.Root)));
        Rows.Add(PathRow("Settings file", _locations.SettingsFile, File.Exists(_locations.SettingsFile)));
        Rows.Add(PathRow("Log folder", _locations.LogsDirectory, Directory.Exists(_locations.LogsDirectory)));
        try
        {
            var hardwareTask = _hardware.GetReportAsync(token);
            var gameTask = _gameLoop.DetectAsync(token);
            await Task.WhenAll(hardwareTask, gameTask);
            if (!_session.IsCurrent(generation))
            {
                return;
            }

            AddHardware(hardwareTask.Result);
            AddScan(gameTask.Result);
            await AddConfigAsync(gameTask.Result, token);
            if (!_session.IsCurrent(generation))
            {
                return;
            }
            var frames = _frames.TryGetLatest();
            if (frames.Succeeded && frames.Value?.FramesPerSecond is double fps && !double.IsNaN(fps) && !double.IsInfinity(fps) && fps >= 0)
            {
                Add("Frame metrics", frames);
            }
            else
            {
                Rows.Add(new DiagnosticRowModel("Frame metrics", Phase0Notices.NoFrameMetrics, "Unavailable", StatusKind.Unavailable));
            }
            await AddMarketAndCodAsync(token);
            if (!_session.IsCurrent(generation))
            {
                return;
            }

            await AddOptimizationAsync(token);
            AddBackups(_backups.List());
        }
        catch (Exception ex)
        {
            if (_session.IsCurrent(generation))
            {
                Rows.Add(new DiagnosticRowModel("Scan", ex.Message, "Needs attention", StatusKind.Attention));
            }
        }
    }

    private void AddHardware(OperationResult<HardwareReport> result)
    {
        if (!result.Succeeded || result.Value is null)
        {
            Rows.Add(new DiagnosticRowModel("Hardware", result.Error ?? "Hardware could not be read.", "Needs attention", StatusKind.Attention));
            return;
        }

        var report = result.Value;
        Rows.Add(Known("CPU", HardwareText.Text(report.CpuName), report.CpuName is not null));
        Rows.Add(Known("GPU", HardwareText.Text(report.GpuName), report.GpuName is not null));
        Rows.Add(Known("Memory", HardwareText.Memory(report.TotalMemoryBytes), report.TotalMemoryBytes is not null));
        Rows.Add(Known("Architecture", HardwareText.Text(report.Architecture), report.Architecture is not null));
    }

    private void AddScan(OperationResult<GameLoopScan> result)
    {
        if (!result.Succeeded || result.Value is null)
        {
            Rows.Add(new DiagnosticRowModel("GameLoop", result.Error ?? "GameLoop could not be scanned.", "Needs attention", StatusKind.Attention));
            Rows.Add(new DiagnosticRowModel("PUBG Mobile", "Unknown", "Unknown", StatusKind.Unavailable));
            return;
        }

        var scan = result.Value;
        var product = DetectionText.ForGameLoop(scan, false);
        Rows.Add(new DiagnosticRowModel(
            "GameLoop",
            scan.Installations.Count == 0 ? "Not found." : scan.Installations[0].InstallPath ?? "Installed.",
            product.Badge,
            product.Kind));
        var pubg = DetectionText.ForMobile(scan.PubgMobile, false);
        Rows.Add(new DiagnosticRowModel("PUBG Mobile", scan.PubgMobile.Detail ?? pubg.Badge, pubg.Badge, pubg.Kind));
    }

    private async Task AddConfigAsync(OperationResult<GameLoopScan> result, CancellationToken token)
    {
        if (!result.Succeeded || result.Value is null)
        {
            Rows.Add(new DiagnosticRowModel("Configuration", "Configuration was not searched.", "Unknown", StatusKind.Unavailable));
            return;
        }

        var paths = result.Value.Installations
            .Select(installation => installation.InstallPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToArray();
        var config = await _config.DiscoverAsync(paths, token);
        if (token.IsCancellationRequested)
        {
            return;
        }

        if (!config.Succeeded || config.Value is null)
        {
            Rows.Add(new DiagnosticRowModel("Configuration", config.Error ?? "Configuration could not be read.", "Needs attention", StatusKind.Attention));
            return;
        }

        var report = config.Value;
        if (report.Installs.Count == 0)
        {
            Rows.Add(new DiagnosticRowModel(
                "Configuration",
                report.Notice ?? "GameLoop was not found, so configuration was not searched.",
                "Unknown",
                StatusKind.Unavailable));
            return;
        }

        var settings = new List<DiagnosticRowModel>();
        ConfigRows.FillSettings(settings, report.Installs[0].Settings);
        foreach (var row in settings)
        {
            Rows.Add(row);
        }

        if (report.SharedFiles.Count > 0)
        {
            Rows.Add(new DiagnosticRowModel(
                "User configuration",
                report.Notice ?? "User configuration is listed separately because more than one install was found.",
                report.SharedSettings is null ? "Unknown" : "Reported",
                report.SharedSettings is null ? StatusKind.Unavailable : StatusKind.Ready));
        }

        var present = 0;
        var unreadable = 0;
        var missing = 0;
        foreach (var file in report.Installs.SelectMany(install => install.Files).Concat(report.SharedFiles))
        {
            switch (file.Presence)
            {
                case ConfigPresence.Present:
                    present++;
                    Rows.Add(ConfigRows.File(file));
                    break;
                case ConfigPresence.Unreadable:
                    unreadable++;
                    Rows.Add(ConfigRows.File(file));
                    break;
                default:
                    missing++;
                    break;
            }
        }

        Rows.Add(new DiagnosticRowModel(
            "Config locations",
            present.ToString(System.Globalization.CultureInfo.InvariantCulture) + " found, "
                + unreadable.ToString(System.Globalization.CultureInfo.InvariantCulture) + " unreadable, "
                + missing.ToString(System.Globalization.CultureInfo.InvariantCulture) + " not found.",
            present > 0 ? "Reported" : "Unknown",
            present > 0 ? StatusKind.Ready : StatusKind.Unavailable));
    }

    private async Task AddMarketAndCodAsync(CancellationToken token)
    {
        var result = await _cod.RunAsync(checkOfficialVersion: false, token);
        if (token.IsCancellationRequested)
        {
            return;
        }

        if (!result.Succeeded || result.Value is null)
        {
            var detail = result.Error ?? "The check could not finish.";
            Rows.Add(new DiagnosticRowModel("APP MARKET", detail, "UNKNOWN", StatusKind.Unavailable));
            Rows.Add(new DiagnosticRowModel("COD MOBILE", detail, "UNKNOWN", StatusKind.Unavailable));
            return;
        }

        var report = result.Value;
        var market = report.Market;
        var comparison = OutcomeFor(market.Comparison);
        Rows.Add(new DiagnosticRowModel(
            "APP MARKET",
            market.Issue ?? market.Detail ?? "No comparison detail was recorded.",
            Badge(comparison),
            KindFor(comparison)));
        Rows.Add(VersionRow("APP MARKET / Installed version", market.InstalledVersion, "The installed version is read from a COD Mobile package folder. Unknown means no unambiguous version file was found."));
        Rows.Add(VersionRow("APP MARKET / Market version", market.MarketVersion, "The market version is read from App Market metadata under the verified install. Unknown means that metadata did not contain one unambiguous version."));
        Rows.Add(new DiagnosticRowModel(
            "APP MARKET / Official version",
            market.OfficialDetail ?? "Official version was not requested.",
            market.OfficialVersion is null ? "UNKNOWN" : "PASS",
            market.OfficialVersion is null ? StatusKind.Unavailable : StatusKind.Ready));
        Rows.Add(new DiagnosticRowModel(
            "APP MARKET / Last scan",
            market.LastScanUtc?.ToString("u", System.Globalization.CultureInfo.InvariantCulture) ?? "Unknown",
            market.LastScanUtc is null ? "UNKNOWN" : "PASS",
            market.LastScanUtc is null ? StatusKind.Unavailable : StatusKind.Ready));

        Rows.Add(new DiagnosticRowModel(
            "COD MOBILE",
            report.InstalledDetail ?? "COD Mobile presence was not reported.",
            DetectionText.Presence(report.InstalledStatus),
            DetectionText.PresenceKind(report.InstalledStatus)));
        foreach (var finding in report.Findings)
        {
            Rows.Add(new DiagnosticRowModel(
                "COD MOBILE / " + finding.Title,
                finding.Evidence + " Recommended action: " + finding.RecommendedAction,
                Badge(finding.Outcome),
                KindFor(finding.Outcome)));
        }
    }

    private static DiagnosticRowModel VersionRow(string title, string? version, string explanation)
    {
        var known = !string.IsNullOrWhiteSpace(version);
        return new DiagnosticRowModel(
            title,
            known ? version + ". " + explanation : "Unknown. " + explanation,
            known ? "PASS" : "UNKNOWN",
            known ? StatusKind.Ready : StatusKind.Unavailable);
    }

    private static FindingOutcome OutcomeFor(CatalogComparison comparison) => comparison switch
    {
        CatalogComparison.Match => FindingOutcome.Pass,
        CatalogComparison.VersionMismatch => FindingOutcome.Failed,
        CatalogComparison.LocalMarketOutdated => FindingOutcome.Warning,
        CatalogComparison.RemoteCatalogIssue => FindingOutcome.Warning,
        _ => FindingOutcome.Unknown
    };

    private static string Badge(FindingOutcome outcome) => outcome switch
    {
        FindingOutcome.Pass => "PASS",
        FindingOutcome.Warning => "WARNING",
        FindingOutcome.Failed => "FAILED",
        _ => "UNKNOWN"
    };

    private static StatusKind KindFor(FindingOutcome outcome) => outcome switch
    {
        FindingOutcome.Pass => StatusKind.Ready,
        FindingOutcome.Warning => StatusKind.Attention,
        FindingOutcome.Failed => StatusKind.Attention,
        _ => StatusKind.Unavailable
    };

    private async Task AddOptimizationAsync(CancellationToken token)
    {
        var analysis = await _optimization.AnalyzeAsync(GLOptimizer.Core.Optimization.OptimizationProfile.Balanced, token);
        if (!analysis.Succeeded || analysis.Value is null)
        {
            Rows.Add(new DiagnosticRowModel("Optimization", analysis.Error ?? "Optimization could not be analyzed.", "Needs attention", StatusKind.Attention));
            return;
        }

        var applicable = analysis.Value.Recommendations.Count(item => item.Status == GLOptimizer.Core.Optimization.RecommendationStatus.Applicable);
        Rows.Add(new DiagnosticRowModel(
            "Optimization",
            applicable.ToString(System.Globalization.CultureInfo.InvariantCulture) + " applicable recommendation(s). Hardware tier: " + analysis.Value.Tier + ".",
            applicable > 0 ? "Ready" : "Reported",
            applicable > 0 ? StatusKind.Ready : StatusKind.Neutral));
    }

    private void AddBackups(OperationResult<IReadOnlyList<GLOptimizer.Core.Models.BackupRecord>> result)
    {
        if (!result.Succeeded || result.Value is null)
        {
            Add("Backups", result);
            return;
        }

        var damaged = result.Value.Count(record => record.Damaged);
        Rows.Add(new DiagnosticRowModel(
            "Backups",
            result.Value.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " backup(s), "
                + damaged.ToString(System.Globalization.CultureInfo.InvariantCulture) + " damaged.",
            damaged > 0 ? "Needs attention" : "Reported",
            damaged > 0 ? StatusKind.Attention : StatusKind.Ready));
    }

    private void Add<T>(string title, OperationResult<T> result)
    {
        Rows.Add(new DiagnosticRowModel(
            title,
            ReportedValue.Detail(result.Succeeded, result.Error),
            StatusMapping.Badge(result.Status),
            StatusMapping.From(result.Status)));
    }

    private static DiagnosticRowModel Known(string title, string value, bool known) =>
        new(title, value, known ? "Reported" : "Unknown", known ? StatusKind.Ready : StatusKind.Unavailable);

    private static DiagnosticRowModel PathRow(string title, string path, bool exists)
    {
        return new DiagnosticRowModel(
            title,
            path,
            exists ? "Ready" : "Missing",
            exists ? StatusKind.Ready : StatusKind.Attention);
    }
}

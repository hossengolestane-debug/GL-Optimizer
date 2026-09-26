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
    private readonly IAppMarketDiagnostics _market;
    private readonly IOptimizationService _optimization;
    private readonly IBackupService _backups;
    private readonly ScanSession _session = new();

    public DiagnosticsViewModel(
        AppDataLocations locations,
        IHardwareService hardware,
        IFrameMetricsProvider frames,
        IGameLoopDetector gameLoop,
        IGameLoopConfigDiscovery config,
        IAppMarketDiagnostics market,
        IOptimizationService optimization,
        IBackupService backups)
        : base(AppPage.Diagnostics)
    {
        _locations = locations;
        _hardware = hardware;
        _frames = frames;
        _gameLoop = gameLoop;
        _config = config;
        _market = market;
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
            Add("App Market", _market.Check());
            Add("Optimization", _optimization.ListActions());
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
            Rows.Add(new DiagnosticRowModel("COD Mobile", "Unknown", "Unknown", StatusKind.Unavailable));
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
        var cod = DetectionText.ForMobile(scan.CodMobile, false);
        Rows.Add(new DiagnosticRowModel("PUBG Mobile", scan.PubgMobile.Detail ?? pubg.Badge, pubg.Badge, pubg.Kind));
        Rows.Add(new DiagnosticRowModel("COD Mobile", scan.CodMobile.Detail ?? cod.Badge, cod.Badge, cod.Kind));
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

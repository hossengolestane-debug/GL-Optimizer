using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.Core.Abstractions;
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
    private readonly IAppMarketDiagnostics _market;
    private readonly IOptimizationService _optimization;
    private readonly IBackupService _backups;
    private readonly ScanSession _session = new();

    public DiagnosticsViewModel(
        AppDataLocations locations,
        IHardwareService hardware,
        IFrameMetricsProvider frames,
        IGameLoopDetector gameLoop,
        IAppMarketDiagnostics market,
        IOptimizationService optimization,
        IBackupService backups)
        : base(AppPage.Diagnostics)
    {
        _locations = locations;
        _hardware = hardware;
        _frames = frames;
        _gameLoop = gameLoop;
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
            Add("Backups", _backups.List());
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

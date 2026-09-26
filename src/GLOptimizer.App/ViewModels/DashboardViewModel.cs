using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Monitoring;
using GLOptimizer.Core.Navigation;
using GLOptimizer.Core.Results;

namespace GLOptimizer.App.ViewModels;

public partial class DashboardViewModel : PageViewModel, IRefreshable
{
    private readonly IHardwareService _hardware;
    private readonly IGameLoopDetector _gameLoop;
    private readonly ILogStore _log;
    private readonly IMonitoringCoordinator _monitoring;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private readonly ScanSession _session = new();
    private HardwareReport? _hardwareReport;
    private bool _hardwareOk;
    private string _hardwareError = "Hardware could not be read.";

    public DashboardViewModel(
        IHardwareService hardware,
        IGameLoopDetector gameLoop,
        ILogStore log,
        IMonitoringCoordinator monitoring)
        : base(AppPage.Dashboard)
    {
        _hardware = hardware;
        _gameLoop = gameLoop;
        _log = log;
        _monitoring = monitoring;
        _monitoring.Updated += OnMonitoringUpdated;
    }

    public string SafetyNote => Phase0Notices.Safety;

    public string LogPath => _log.ActiveLogFilePath;

    public ObservableCollection<MetricTile> Cards { get; } =
    [
        new("CPU"),
        new("GPU"),
        new("RAM"),
        new("Disk")
    ];

    public ObservableCollection<ActivityRow> Activity { get; } = new();

    public ObservableCollection<DiagnosticRowModel> HardwareRows { get; } = new();

    public ObservableCollection<GameStatusCard> GameCards { get; } = new();

    public ObservableCollection<InstallItem> Installs { get; } = new();

    public bool HasActivity => Activity.Count > 0;

    public bool HasInstalls => Installs.Count > 0;

    public string ChartTitle => "Frame time";

    public string ChartSubtitle => "FPS is not collected.";

    public string ChartEmptyMessage => Phase0Notices.NoFrameMetrics;

    public bool ChartHasData => false;

    [ObservableProperty]
    private string _statusLine = "Scanning…";

    [ObservableProperty]
    private string _overallText = "SCANNING";

    [ObservableProperty]
    private StatusKind _overallKind = StatusKind.Neutral;

    [ObservableProperty]
    private bool _isScanning;

    [RelayCommand(CanExecute = nameof(CanOptimize))]
    private void OptimizeNow()
    {
        _log.Write(LogSeverity.Warning, "Optimize", "OPTIMIZE NOW was invoked, but optimization is not implemented.");
    }

    private static bool CanOptimize() => false;

    [RelayCommand]
    private Task ScanAsync() => RunScanAsync();

    [RelayCommand]
    private void CancelScan() => _session.Cancel();

    public void Refresh() => _ = RunScanAsync();

    private async Task RunScanAsync()
    {
        var (generation, token) = _session.Start();
        try
        {
            IsScanning = true;
            StatusLine = "Scanning…";
            var hardwareTask = _hardware.GetReportAsync(token);
            var gameTask = _gameLoop.DetectAsync(token);
            await Task.WhenAll(hardwareTask, gameTask);
            if (!_session.IsCurrent(generation))
            {
                return;
            }

            Apply(hardwareTask.Result, gameTask.Result);
        }
        catch (Exception ex)
        {
            if (!_session.IsCurrent(generation))
            {
                return;
            }

            StatusLine = "The scan could not finish.";
            _log.Write(LogSeverity.Error, "Scan", ex.Message);
        }
        finally
        {
            if (_session.IsCurrent(generation))
            {
                IsScanning = false;
            }
        }
    }

    private void Apply(
        OperationResult<HardwareReport> hardware,
        OperationResult<GameLoopScan> scan)
    {
        var report = hardware.Succeeded ? hardware.Value : null;
        var found = scan.Succeeded ? scan.Value : null;
        var state = DiagnosticAssessment.Evaluate(report, found, !hardware.Succeeded, !scan.Succeeded);
        OverallText = DetectionText.State(state);
        OverallKind = DetectionText.StateKind(state);
        StatusLine = scan.Succeeded
            ? found!.Installations.Count == 0
                ? "GameLoop was not found."
                : found.Installations.Count.ToString(CultureInfo.InvariantCulture) + " GameLoop installation(s) found."
            : scan.Error ?? "GameLoop could not be scanned.";

        _hardwareReport = report;
        _hardwareOk = hardware.Succeeded;
        _hardwareError = hardware.Error ?? "Hardware could not be read.";
        var gameLoopStatus = DetectionText.ForGameLoop(found, !scan.Succeeded);
        RenderCards();

        HardwareRows.Clear();
        AddHardware(report);
        if (!hardware.Succeeded)
        {
            HardwareRows.Insert(0, new DiagnosticRowModel("Hardware", hardware.Error ?? "Hardware could not be read.", "Needs attention", StatusKind.Attention));
        }

        GameCards.Clear();
        GameCards.Add(new GameStatusCard("GameLoop", "Verified install", gameLoopStatus.Badge, StatusLine, "G", gameLoopStatus.Kind));
        var pubg = DetectionText.ForMobile(found?.PubgMobile, !scan.Succeeded);
        var cod = DetectionText.ForMobile(found?.CodMobile, !scan.Succeeded);
        GameCards.Add(new GameStatusCard(
            "PUBG Mobile",
            HardwareText.Text(found?.PubgMobile.PackageId),
            pubg.Badge,
            found?.PubgMobile.Detail ?? "Unknown",
            "P",
            pubg.Kind));
        GameCards.Add(new GameStatusCard(
            "COD Mobile",
            HardwareText.Text(found?.CodMobile.PackageId),
            cod.Badge,
            found?.CodMobile.Detail ?? "Unknown",
            "C",
            cod.Kind));

        Installs.Clear();
        if (found is not null)
        {
            foreach (var installation in found.Installations)
            {
                Installs.Add(InstallItem.From(installation));
            }
        }

        OnPropertyChanged(nameof(HasInstalls));
        _log.Write(LogSeverity.Information, "Scan", OverallText + ". " + StatusLine);
        ReloadActivity();
    }

    private void OnMonitoringUpdated(object? sender, EventArgs e)
    {
        if (_ui is null)
        {
            RenderCards();
            return;
        }

        _ui.Post(_ => RenderCards(), null);
    }

    private void RenderCards()
    {
        var live = _monitoring.IsRunning ? _monitoring.Latest : null;
        var cpuName = HardwareText.Text(_hardwareReport?.CpuName);
        var gpuName = HardwareText.Text(_hardwareReport?.GpuName);
        if (live is null)
        {
            Cards[0].Set(
                cpuName,
                _hardwareOk ? "Reported by this PC." : _hardwareError,
                _hardwareReport?.CpuName is null ? "Unknown" : "Reported",
                _hardwareReport?.CpuName is null ? StatusKind.Unavailable : StatusKind.Ready);
            Cards[1].Set(
                gpuName,
                _hardwareReport?.GpuName is null ? "GPU was not reported." : "Reported by this PC.",
                _hardwareReport?.GpuName is null ? "Unknown" : "Reported",
                _hardwareReport?.GpuName is null ? StatusKind.Unavailable : StatusKind.Ready);
            Cards[2].Set(
                HardwareText.Memory(_hardwareReport?.TotalMemoryBytes),
                "Total physical memory.",
                _hardwareReport?.TotalMemoryBytes is > 0 ? "Reported" : "Unknown",
                _hardwareReport?.TotalMemoryBytes is > 0 ? StatusKind.Ready : StatusKind.Unavailable);
            Cards[3].Set(
                HardwareText.Text(_hardwareReport?.StorageType),
                "Storage type. Disk activity is sampled only while monitoring is running.",
                _hardwareReport?.StorageType is null ? "Unknown" : "Reported",
                _hardwareReport?.StorageType is null ? StatusKind.Unavailable : StatusKind.Ready);
            return;
        }

        Cards[0].Set(MetricText.Percent(live.CpuPercent), cpuName, live.CpuPercent is null ? "Unknown" : "Live", live.CpuPercent is null ? StatusKind.Unavailable : StatusKind.Ready);
        var gpuDetail = live.GpuVramUsedBytes is null ? gpuName : "VRAM " + MetricText.Bytes(live.GpuVramUsedBytes);
        Cards[1].Set(MetricText.Percent(live.GpuPercent), gpuDetail, live.GpuPercent is null ? "Unknown" : "Live", live.GpuPercent is null ? StatusKind.Unavailable : StatusKind.Ready);
        Cards[2].Set(
            MetricText.RamPair(live.RamUsedBytes, live.RamTotalBytes),
            MetricText.Percent(live.RamPercent) + " used",
            live.RamPercent is null ? "Unknown" : "Live",
            live.RamPercent is null ? StatusKind.Unavailable : StatusKind.Ready);
        Cards[3].Set(MetricText.Percent(live.DiskPercent), "Disk activity", live.DiskPercent is null ? "Unknown" : "Live", live.DiskPercent is null ? StatusKind.Unavailable : StatusKind.Ready);
    }

    private void AddHardware(HardwareReport? report)
    {
        HardwareRows.Add(Row("CPU", HardwareText.Text(report?.CpuName), report?.CpuName is not null));
        HardwareRows.Add(Row("Physical cores", HardwareText.Count(report?.PhysicalCores), report?.PhysicalCores is not null));
        HardwareRows.Add(Row("Logical cores", HardwareText.Count(report?.LogicalCores), report?.LogicalCores is not null));
        HardwareRows.Add(Row("GPU", HardwareText.Text(report?.GpuName), report?.GpuName is not null));
        HardwareRows.Add(Row("VRAM", HardwareText.Memory(report?.GpuMemoryBytes), report?.GpuMemoryBytes is not null));
        HardwareRows.Add(Row("Memory", HardwareText.Memory(report?.TotalMemoryBytes), report?.TotalMemoryBytes is not null));
        HardwareRows.Add(Row("Storage", HardwareText.Text(report?.StorageType), report?.StorageType is not null));
        HardwareRows.Add(Row("Refresh", HardwareText.Hertz(report?.MonitorRefreshHz), report?.MonitorRefreshHz is not null));
        HardwareRows.Add(Row("Windows", HardwareText.Text(report?.WindowsVersion), report?.WindowsVersion is not null));
        HardwareRows.Add(Row("Build", HardwareText.Text(report?.WindowsBuild), report?.WindowsBuild is not null));
        HardwareRows.Add(Row("Architecture", HardwareText.Text(report?.Architecture), report?.Architecture is not null));
        HardwareRows.Add(Row("Virtualization", HardwareText.Flag(report?.VirtualizationFirmwareEnabled, "Enabled", "Disabled"), report?.VirtualizationFirmwareEnabled is not null));
        HardwareRows.Add(Row("Hypervisor", HardwareText.Flag(report?.HypervisorPresent, "Present", "Not present"), report?.HypervisorPresent is not null));
    }

    private static DiagnosticRowModel Row(string title, string value, bool known) =>
        new(title, value, known ? "Reported" : "Unknown", known ? StatusKind.Ready : StatusKind.Unavailable);

    private void ReloadActivity()
    {
        Activity.Clear();
        foreach (var entry in _log.GetRecent(8).Reverse())
        {
            Activity.Add(new ActivityRow(
                entry.Timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture),
                entry.Category,
                entry.Message));
        }

        OnPropertyChanged(nameof(HasActivity));
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Navigation;

namespace GLOptimizer.App.ViewModels;

public partial class MonitoringViewModel : PageViewModel, IRefreshable
{
    private readonly IHardwareService _hardware;
    private readonly IFrameMetricsProvider _frames;
    private readonly ScanSession _session = new();

    public MonitoringViewModel(IHardwareService hardware, IFrameMetricsProvider frames)
        : base(AppPage.Monitoring)
    {
        _hardware = hardware;
        _frames = frames;
    }

    public ObservableCollection<DashboardCard> Cards { get; } = new();

    public ObservableCollection<DiagnosticRowModel> HardwareRows { get; } = new();

    public string ChartTitle => "Frame time";

    public string ChartSubtitle => "No capture is running.";

    public string ChartEmptyMessage => Phase0Notices.NoFrameMetrics;

    public bool ChartHasData => false;

    public string SafetyNote => Phase0Notices.Safety;

    [ObservableProperty]
    private string _statusLine = "Scanning…";

    [RelayCommand]
    private void CancelScan() => _session.Cancel();

    public void Refresh() => _ = RunScanAsync();

    private async Task RunScanAsync()
    {
        var (generation, token) = _session.Start();
        try
        {
            StatusLine = "Scanning…";
            var hardwareTask = _hardware.GetReportAsync(token);
            var frames = _frames.TryGetLatest();
            var hardware = await hardwareTask;
            if (!_session.IsCurrent(generation))
            {
                return;
            }

            var report = hardware.Succeeded ? hardware.Value : null;
            StatusLine = hardware.Succeeded ? "Hardware scan finished." : hardware.Error ?? "Hardware could not be read.";
            Cards.Clear();
            Cards.Add(Card("CPU", HardwareText.Text(report?.CpuName), report?.CpuName is not null));
            Cards.Add(Card("Memory", HardwareText.Memory(report?.TotalMemoryBytes), report?.TotalMemoryBytes is > 0));
            Cards.Add(Card("GPU", HardwareText.Text(report?.GpuName), report?.GpuName is not null));
            Cards.Add(new DashboardCard(
                "Frames",
                ReportedValue.FramesPerSecond(frames),
                Phase0Notices.NoFrameMetrics,
                StatusMapping.Badge(frames.Status),
                StatusMapping.From(frames.Status)));

            HardwareRows.Clear();
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
        }
        catch (Exception ex)
        {
            if (_session.IsCurrent(generation))
            {
                StatusLine = "The scan could not finish. " + ex.Message;
            }
        }
    }

    private static DashboardCard Card(string title, string value, bool known) =>
        new(title, value, known ? "Reported by this PC." : "Not reported.", known ? "Reported" : "Unknown", known ? StatusKind.Ready : StatusKind.Unavailable);

    private static DiagnosticRowModel Row(string title, string value, bool known) =>
        new(title, value, known ? "Reported" : "Unknown", known ? StatusKind.Ready : StatusKind.Unavailable);
}

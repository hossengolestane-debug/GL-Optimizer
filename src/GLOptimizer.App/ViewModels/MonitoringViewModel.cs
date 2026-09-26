using System.Collections.ObjectModel;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Navigation;

namespace GLOptimizer.App.ViewModels;

public partial class MonitoringViewModel : PageViewModel, IRefreshable
{
    private readonly IHardwareService _hardware;
    private readonly IFrameMetricsProvider _frames;

    public MonitoringViewModel(IHardwareService hardware, IFrameMetricsProvider frames)
        : base(AppPage.Monitoring)
    {
        _hardware = hardware;
        _frames = frames;
    }

    public ObservableCollection<DashboardCard> Cards { get; } = new();

    public string ChartTitle => "Frame time";

    public string ChartSubtitle => "No capture is running.";

    public string ChartEmptyMessage => Phase0Notices.NoLiveMetrics;

    public bool ChartHasData => false;

    public string SafetyNote => Phase0Notices.Safety;

    public void Refresh()
    {
        var hardware = _hardware.TryGetReport();
        var frames = _frames.TryGetLatest();
        Cards.Clear();
        Cards.Add(new DashboardCard(
            "Hardware",
            ReportedValue.HardwareCpu(hardware),
            ReportedValue.Detail(hardware.Succeeded, hardware.Error),
            StatusMapping.Badge(hardware.Status),
            StatusMapping.From(hardware.Status)));
        Cards.Add(new DashboardCard(
            "Frames",
            ReportedValue.FramesPerSecond(frames),
            ReportedValue.Detail(frames.Succeeded, frames.Error),
            StatusMapping.Badge(frames.Status),
            StatusMapping.From(frames.Status)));
    }
}

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Navigation;

namespace GLOptimizer.App.ViewModels;

public partial class DashboardViewModel : PageViewModel, IRefreshable
{
    private readonly IHardwareService _hardware;
    private readonly IFrameMetricsProvider _frames;
    private readonly IGameLoopDetector _gameLoop;
    private readonly IBackupService _backups;
    private readonly ILogStore _log;

    public DashboardViewModel(
        IHardwareService hardware,
        IFrameMetricsProvider frames,
        IGameLoopDetector gameLoop,
        IBackupService backups,
        ILogStore log)
        : base(AppPage.Dashboard)
    {
        _hardware = hardware;
        _frames = frames;
        _gameLoop = gameLoop;
        _backups = backups;
        _log = log;
    }

    public string SafetyNote => Phase0Notices.Safety;

    public string LogPath => _log.ActiveLogFilePath;

    public ObservableCollection<DashboardCard> Cards { get; } = new();

    public ObservableCollection<ActivityRow> Activity { get; } = new();

    public bool HasActivity => Activity.Count > 0;

    public string ChartTitle => "Performance";

    public string ChartSubtitle => "No capture is running.";

    public string ChartEmptyMessage => Phase0Notices.NoLiveMetrics;

    public bool ChartHasData => false;

    [RelayCommand(CanExecute = nameof(CanOptimize))]
    private void OptimizeNow()
    {
        _log.Write(LogSeverity.Warning, "Optimize", "OPTIMIZE NOW was invoked, but optimization is not implemented.");
    }

    private static bool CanOptimize() => false;

    public void Refresh()
    {
        var hardware = _hardware.TryGetReport();
        var frames = _frames.TryGetLatest();
        var gameLoop = _gameLoop.Detect();
        var backups = _backups.List();

        Cards.Clear();
        Cards.Add(new DashboardCard(
            "Hardware",
            ReportedValue.HardwareCpu(hardware),
            ReportedValue.Detail(hardware.Succeeded, hardware.Error),
            StatusMapping.Badge(hardware.Status),
            StatusMapping.From(hardware.Status)));
        Cards.Add(new DashboardCard(
            "Frame metrics",
            ReportedValue.FramesPerSecond(frames),
            ReportedValue.Detail(frames.Succeeded, frames.Error),
            StatusMapping.Badge(frames.Status),
            StatusMapping.From(frames.Status)));
        Cards.Add(new DashboardCard(
            "GameLoop",
            ReportedValue.GameLoopVersion(gameLoop),
            ReportedValue.Detail(gameLoop.Succeeded, gameLoop.Error) + " " + Phase0Notices.NoGameLoopIo,
            StatusMapping.Badge(gameLoop.Status),
            StatusMapping.From(gameLoop.Status)));
        Cards.Add(new DashboardCard(
            "Backups",
            ReportedValue.BackupCount(backups),
            ReportedValue.Detail(backups.Succeeded, backups.Error),
            StatusMapping.Badge(backups.Status),
            StatusMapping.From(backups.Status)));

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

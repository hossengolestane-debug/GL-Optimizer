using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Diagnostics;
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

    public string SafetyNote => Phase0Notices.Safety;

    [RelayCommand]
    private void RunChecks() => Refresh();

    public void Refresh()
    {
        Rows.Clear();
        Rows.Add(PathRow("App data", _locations.Root, Directory.Exists(_locations.Root)));
        Rows.Add(PathRow("Settings file", _locations.SettingsFile, File.Exists(_locations.SettingsFile)));
        Rows.Add(PathRow("Log folder", _locations.LogsDirectory, Directory.Exists(_locations.LogsDirectory)));
        Add("Hardware", _hardware.TryGetReport());
        Add("Frame metrics", _frames.TryGetLatest());
        Add("GameLoop", _gameLoop.Detect());
        Add("App Market", _market.Check());
        Add("Optimization", _optimization.ListActions());
        Add("Backups", _backups.List());
    }

    private void Add<T>(string title, OperationResult<T> result)
    {
        Rows.Add(new DiagnosticRowModel(
            title,
            ReportedValue.Detail(result.Succeeded, result.Error),
            StatusMapping.Badge(result.Status),
            StatusMapping.From(result.Status)));
    }

    private static DiagnosticRowModel PathRow(string title, string path, bool exists)
    {
        return new DiagnosticRowModel(
            title,
            path,
            exists ? "Ready" : "Missing",
            exists ? StatusKind.Ready : StatusKind.Attention);
    }
}

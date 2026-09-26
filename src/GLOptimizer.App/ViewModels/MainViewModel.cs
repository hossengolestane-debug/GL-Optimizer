using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.App.Services;
using GLOptimizer.Core;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Navigation;
using GLOptimizer.Core.Results;
using GLOptimizer.Core.Optimization;
using GLOptimizer.Core.Recovery;

namespace GLOptimizer.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly INavigationService _navigation;
    private readonly IPageViewModelFactory _pages;
    private readonly ISettingsStore _settings;
    private readonly ILogStore _log;
    private readonly IMonitoringCoordinator _monitoring;
    private readonly IHardwareService _hardware;
    private readonly IGameLoopDetector _detector;
    private readonly IOptimizationService _optimization;
    private readonly ILaunchOptimized _launch;
    private readonly IRepairStateStore _repair;
    private readonly IOptimizationRecordStore _records;
    private readonly IAppMarketRepair _marketRepair;
    private readonly IToastCenter _toasts;
    private readonly EventHandler _onToast;
    private readonly EventHandler<AppPage> _onPage;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private bool _persistSidebar;
    private bool _showingPage;
    private RecoveryKind _recoveryKind;

    public MainViewModel(
        INavigationService navigation,
        IPageViewModelFactory pages,
        ISettingsStore settings,
        ILogStore log,
        IMonitoringCoordinator monitoring,
        IHardwareService hardware,
        IGameLoopDetector detector,
        IOptimizationService optimization,
        ILaunchOptimized launch,
        IRepairStateStore repair,
        IOptimizationRecordStore records,
        IAppMarketRepair marketRepair,
        IToastCenter toasts)
    {
        _navigation = navigation;
        _pages = pages;
        _settings = settings;
        _log = log;
        _monitoring = monitoring;
        _hardware = hardware;
        _detector = detector;
        _optimization = optimization;
        _launch = launch;
        _repair = repair;
        _records = records;
        _marketRepair = marketRepair;
        _toasts = toasts;
        _onToast = (_, _) => Post(() => ToastText = _toasts.Current);
        _toasts.Changed += _onToast;
        Sections = PageCatalog.All
            .GroupBy(page => page.Group)
            .Select(group => new NavigationSectionViewModel
            {
                Title = group.Key,
                Items = group.Select(page => new NavigationItemViewModel(page.Page, page.Title, page.Group, PageIcons.For(page.Page))).ToList()
            })
            .ToList();
        IsSidebarCollapsed = settings.Current.SidebarCollapsed;
        _persistSidebar = true;
        _onPage = (_, page) =>
        {
            if (!_showingPage)
            {
                Show(page);
            }
        };
        _navigation.CurrentChanged += _onPage;
        Navigate(AppPage.Dashboard);
        LoadRecovery();
        if (!SmokeTest.Active && !_settings.Current.FirstRunCompleted)
        {
            ShowFirstRun = true;
            FirstRunHeadline = "Welcome to GL Optimizer";
            FirstRunBody = "Scanning your system...";
            _ = ScanFirstRunAsync();
        }
    }

    public IReadOnlyList<NavigationSectionViewModel> Sections { get; }

    public string VersionLabel => $"{BuildInfo.PhaseName}  {BuildInfo.Version}";

    public string PhaseLabel => BuildInfo.PhaseName;

    public bool MinimizeToTray => _settings.Current.MinimizeToTray;

    public bool AllowExit { get; set; }

    public string CollapseGlyph => IsSidebarCollapsed ? "\uE76B" : "\uE76C";

    public string CollapseToolTip => IsSidebarCollapsed ? "Expand sidebar" : "Collapse sidebar";

    [ObservableProperty]
    private bool _isSidebarCollapsed;

    [ObservableProperty]
    private IPageViewModel? _currentViewModel;

    [ObservableProperty]
    private string? _toastText;

    public bool HasToast => !string.IsNullOrWhiteSpace(ToastText);

    partial void OnToastTextChanged(string? value) => OnPropertyChanged(nameof(HasToast));

    [ObservableProperty]
    private bool _showRecovery;

    [ObservableProperty]
    private string _recoveryMessage = string.Empty;

    [ObservableProperty]
    private string _recoveryAction = "Restore priorities";

    [ObservableProperty]
    private bool _showFirstRun;

    [ObservableProperty]
    private string _firstRunHeadline = "Welcome to GL Optimizer";

    [ObservableProperty]
    private string _firstRunBody = "Scanning your system...";

    [RelayCommand]
    private void Navigate(AppPage page)
    {
        _showingPage = true;
        try
        {
            _navigation.Navigate(page);
            Show(page);
        }
        finally
        {
            _showingPage = false;
        }
    }

    private void Show(AppPage page)
    {
        _monitoring.SetPage(page);
        CurrentViewModel = _pages.Create(page);
        foreach (var item in Sections.SelectMany(section => section.Items))
        {
            item.IsSelected = item.Page == page;
        }
    }

    [RelayCommand]
    private async Task RestoreRecoveryAsync()
    {
        var result = await RunRecoveryAsync();
        if (!result.Succeeded)
        {
            RecoveryMessage = result.Error ?? "Recovery could not finish.";
            return;
        }

        ShowRecovery = false;
        RecoveryMessage = result.Error ?? "Recovery finished.";
    }

    [RelayCommand]
    private async Task DismissRecoveryAsync()
    {
        if (_recoveryKind == RecoveryKind.Launch)
        {
            await _launch.RecoverAsync(restore: false);
        }

        ShowRecovery = false;
    }

    [RelayCommand]
    private void DismissFirstRun()
    {
        var settings = _settings.Current;
        settings.FirstRunCompleted = true;
        var result = _settings.Save(settings);
        if (!result.Succeeded)
        {
            FirstRunBody = result.Error ?? "The welcome result could not be saved.";
            return;
        }

        ShowFirstRun = false;
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    public void Dispose()
    {
        _toasts.Changed -= _onToast;
        _navigation.CurrentChanged -= _onPage;
    }

    private void LoadRecovery()
    {
        var record = _records.Read();
        var decision = StartupRecoveryLogic.Inspect(
            _repair.Load(),
            record.Succeeded ? record.Value : null,
            _launch.Inspect());
        _recoveryKind = decision.Kind;
        ShowRecovery = decision.Kind != RecoveryKind.None;
        RecoveryMessage = decision.Message;
        RecoveryAction = string.IsNullOrWhiteSpace(decision.ActionLabel) ? "Restore priorities" : decision.ActionLabel;
    }

    private async Task<OperationResult> RunRecoveryAsync()
    {
        switch (_recoveryKind)
        {
            case RecoveryKind.Repair:
                var rolled = await _marketRepair.RollbackInterruptedAsync(confirmed: true);
                return rolled.Succeeded
                    ? OperationResult.Success()
                    : OperationResult.Failure(rolled.Error ?? "The interrupted repair could not be rolled back.");
            case RecoveryKind.Optimization:
                var restored = await _optimization.UndoLastAsync(confirmed: true);
                return restored.Succeeded
                    ? OperationResult.Success()
                    : OperationResult.Failure(restored.Error ?? "The pre-apply backup could not be restored.");
            case RecoveryKind.Launch:
                return await _launch.RecoverAsync(restore: true);
            default:
                return OperationResult.Failure("Nothing is waiting to be recovered.");
        }
    }

    private async Task ScanFirstRunAsync()
    {
        try
        {
            var hardwareTask = _hardware.GetReportAsync();
            var gameTask = _detector.DetectAsync();
            await Task.WhenAll(hardwareTask, gameTask);
            var analysis = await _optimization.AnalyzeAsync(OptimizationProfile.Balanced);
            var count = analysis.Succeeded && analysis.Value is not null
                ? analysis.Value.Recommendations.Count(item => item.Status == RecommendationStatus.Applicable)
                : 0;
            var summary = FirstRunSummary.Create(
                hardwareTask.Result.Succeeded ? hardwareTask.Result.Value : null,
                gameTask.Result.Succeeded ? gameTask.Result.Value : null,
                count);
            Post(() => FirstRunBody = summary.Text);
        }
        catch (Exception)
        {
            Post(() => FirstRunBody = "The scan could not finish. You can continue.");
        }
    }

    private void Post(Action action)
    {
        if (_ui is null || SynchronizationContext.Current == _ui)
        {
            action();
            return;
        }

        _ui.Post(_ => action(), null);
    }

    partial void OnIsSidebarCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(CollapseGlyph));
        OnPropertyChanged(nameof(CollapseToolTip));
        if (!_persistSidebar)
        {
            return;
        }

        var settings = _settings.Current;
        settings.SidebarCollapsed = value;
        var result = _settings.Save(settings);
        if (!result.Succeeded)
        {
            _log.Write(LogSeverity.Warning, "Settings", result.Error ?? "Could not save the sidebar state.");
        }
    }
}

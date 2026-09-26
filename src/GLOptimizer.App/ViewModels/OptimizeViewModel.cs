using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Navigation;

namespace GLOptimizer.App.ViewModels;

public partial class OptimizeViewModel : PageViewModel, IRefreshable
{
    private readonly IOptimizationService _optimization;
    private readonly ILogStore _log;

    public OptimizeViewModel(IOptimizationService optimization, ILogStore log)
        : base(AppPage.Optimize)
    {
        _optimization = optimization;
        _log = log;
    }

    public string PanelTitle => "Optimization";

    public double Progress => 0;

    public string SafetyNote => Phase0Notices.Safety;

    [ObservableProperty]
    private string _panelMessage = Phase0Notices.NoOptimization;

    [ObservableProperty]
    private string _actionsMessage = Phase0Notices.NoOptimization;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void Run()
    {
        _log.Write(LogSeverity.Warning, "Optimize", "A run was requested, but optimization is not implemented.");
    }

    private static bool CanRun() => false;

    public void Refresh()
    {
        var actions = _optimization.ListActions();
        ActionsMessage = actions.Error ?? Phase0Notices.NoOptimization;
        PanelMessage = Phase0Notices.NoOptimization;
    }
}

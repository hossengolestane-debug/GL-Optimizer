using CommunityToolkit.Mvvm.ComponentModel;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Navigation;

namespace GLOptimizer.App.ViewModels;

public partial class GameLoopViewModel : PageViewModel, IRefreshable
{
    private readonly IGameLoopDetector _detector;

    public GameLoopViewModel(IGameLoopDetector detector)
        : base(AppPage.GameLoop)
    {
        _detector = detector;
    }

    public string SafetyNote => Phase0Notices.Safety + " " + Phase0Notices.NoGameLoopIo;

    [ObservableProperty]
    private string _message = Phase0Notices.NoGameLoopIo;

    public void Refresh()
    {
        var result = _detector.Detect();
        Message = string.IsNullOrWhiteSpace(result.Error)
            ? Phase0Notices.NoGameLoopIo
            : result.Error + " " + Phase0Notices.NoGameLoopIo;
    }
}

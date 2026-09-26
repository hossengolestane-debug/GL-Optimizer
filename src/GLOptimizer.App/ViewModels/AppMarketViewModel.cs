using CommunityToolkit.Mvvm.ComponentModel;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Navigation;

namespace GLOptimizer.App.ViewModels;

public partial class AppMarketViewModel : PageViewModel, IRefreshable
{
    private readonly IAppMarketDiagnostics _diagnostics;

    public AppMarketViewModel(IAppMarketDiagnostics diagnostics)
        : base(AppPage.AppMarket)
    {
        _diagnostics = diagnostics;
    }

    public string SafetyNote => Phase0Notices.Safety + " " + Phase0Notices.NoAppMarketIo;

    [ObservableProperty]
    private string _message = Phase0Notices.NoAppMarketIo;

    public void Refresh()
    {
        var result = _diagnostics.Check();
        Message = string.IsNullOrWhiteSpace(result.Error)
            ? Phase0Notices.NoAppMarketIo
            : result.Error + " " + Phase0Notices.NoAppMarketIo;
    }
}

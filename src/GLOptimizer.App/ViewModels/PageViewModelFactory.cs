using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Navigation;
using Microsoft.Extensions.DependencyInjection;

namespace GLOptimizer.App.ViewModels;

public sealed class PageViewModelFactory : IPageViewModelFactory, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly Dictionary<AppPage, IPageViewModel> _cache = new();

    public PageViewModelFactory(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    public IPageViewModel Create(AppPage page)
    {
        if (_cache.TryGetValue(page, out var existing))
        {
            Refresh(existing);
            return existing;
        }

        IPageViewModel created = page switch
        {
            AppPage.Dashboard => ActivatorUtilities.CreateInstance<DashboardViewModel>(_services),
            AppPage.Optimize => ActivatorUtilities.CreateInstance<OptimizeViewModel>(_services),
            AppPage.Monitoring => ActivatorUtilities.CreateInstance<MonitoringViewModel>(_services),
            AppPage.GameLoop => ActivatorUtilities.CreateInstance<GameLoopViewModel>(_services),
            AppPage.AppMarket => ActivatorUtilities.CreateInstance<AppMarketViewModel>(_services),
            AppPage.CodMobile => ActivatorUtilities.CreateInstance<GamePageViewModel>(_services, page),
            AppPage.PubgMobile => ActivatorUtilities.CreateInstance<GamePageViewModel>(_services, page),
            AppPage.Diagnostics => ActivatorUtilities.CreateInstance<DiagnosticsViewModel>(_services),
            AppPage.Backups => ActivatorUtilities.CreateInstance<BackupsViewModel>(_services),
            AppPage.Logs => ActivatorUtilities.CreateInstance<LogsViewModel>(_services),
            AppPage.Activity => ActivatorUtilities.CreateInstance<ActivityViewModel>(_services),
            AppPage.Settings => ActivatorUtilities.CreateInstance<SettingsViewModel>(_services),
            _ => throw new ArgumentOutOfRangeException(nameof(page), page, "Unknown page.")
        };

        _cache[page] = created;
        Refresh(created);
        return created;
    }

    public void Dispose()
    {
        foreach (var page in _cache.Values)
        {
            if (page is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _cache.Clear();
    }

    private static void Refresh(IPageViewModel page)
    {
        if (SmokeTest.Active || page is not IRefreshable refreshable)
        {
            return;
        }

        refreshable.Refresh();
    }
}

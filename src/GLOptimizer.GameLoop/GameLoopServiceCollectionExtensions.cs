using GLOptimizer.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace GLOptimizer.GameLoop;

public static class GameLoopServiceCollectionExtensions
{
    public static IServiceCollection AddGameLoopModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IGameLoopEnvironment, WindowsGameLoopEnvironment>();
        services.AddSingleton<IGameLoopDetector, GameLoopDetector>();
        services.AddSingleton<IGameLoopConfigReader, WindowsGameLoopConfigReader>();
        services.AddSingleton<IGameLoopConfigDiscovery, GameLoopConfigDiscovery>();
        services.AddSingleton<IBackupSource, GameLoopBackupSource>();
        services.AddSingleton<IOptimizationService, OptimizationEngine>();
        services.AddSingleton<IProcessStarter, WindowsProcessStarter>();
        services.AddSingleton<IGameLoopLauncher, GameLoopLauncher>();
        services.AddSingleton<IOfficialVersionSource, UnavailableOfficialVersionSource>();
        services.AddSingleton<IWindowTitleSource, WindowTitleProbe>();
        services.AddSingleton<AppMarketDetector>();
        services.AddSingleton<AppMarketCacheManager>();
        services.AddSingleton<AppMarketVersionService>();
        services.AddSingleton<CodMobileVersionChecker>();
        services.AddSingleton<CodMobileDetector>();
        services.AddSingleton<CodMobileLaunchDiagnostics>();
        services.AddSingleton<IAppMarketDiagnostics, AppMarketDiagnostics>();
        services.AddSingleton<ICodMobileDiagnostics, CodMobileDiagnostics>();
        return services;
    }
}

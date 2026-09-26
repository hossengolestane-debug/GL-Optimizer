using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Elevation;
using GLOptimizer.Core.Launch;
using Microsoft.Extensions.DependencyInjection;

namespace GLOptimizer.GameLoop;

public static class GameLoopServiceCollectionExtensions
{
    public static IServiceCollection AddGameLoopModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IInstallOverrideSource, SettingsInstallOverride>();
        services.AddSingleton<IGameLoopEnvironment, WindowsGameLoopEnvironment>();
        services.AddSingleton<IGameLoopDetector, GameLoopDetector>();
        services.AddSingleton<IGameLoopConfigReader, WindowsGameLoopConfigReader>();
        services.AddSingleton<IGameLoopConfigDiscovery, GameLoopConfigDiscovery>();
        services.AddSingleton<IBackupSource, GameLoopBackupSource>();
        services.AddSingleton<IOptimizationService, OptimizationEngine>();
        services.AddSingleton<IProcessStarter, WindowsProcessStarter>();
        services.AddSingleton<IProcessControl, WindowsProcessControl>();
        services.AddSingleton<GameLoopSessionStopper>();
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
        services.AddSingleton<IAppMarketRepair, AppMarketRepairService>();
        services.AddSingleton<IProcessPriority, WindowsProcessPriority>();
        services.AddSingleton<ILaunchOptimized, LaunchOptimizedService>();
        services.AddSingleton<LaunchSessionWatcher>();
        services.AddSingleton<IElevationRelaunch, WindowsElevationRelaunch>();
        services.AddSingleton<IPubgMobileDiagnostics, PubgMobileDiagnostics>();
        return services;
    }
}

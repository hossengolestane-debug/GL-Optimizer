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
        services.AddSingleton<IProcessStarter, WindowsProcessStarter>();
        services.AddSingleton<IGameLoopLauncher, GameLoopLauncher>();
        services.AddSingleton<IAppMarketDiagnostics, NotImplementedAppMarketDiagnostics>();
        return services;
    }
}

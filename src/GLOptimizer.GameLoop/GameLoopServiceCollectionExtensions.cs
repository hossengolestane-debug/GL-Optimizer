using GLOptimizer.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace GLOptimizer.GameLoop;

public static class GameLoopServiceCollectionExtensions
{
    public static IServiceCollection AddGameLoopModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IGameLoopDetector, NotImplementedGameLoopDetector>();
        services.AddSingleton<IAppMarketDiagnostics, NotImplementedAppMarketDiagnostics>();
        return services;
    }
}

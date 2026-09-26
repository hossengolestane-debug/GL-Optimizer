using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Launch;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Navigation;
using GLOptimizer.Core.Optimization;
using GLOptimizer.Infrastructure.Backup;
using GLOptimizer.Infrastructure.Repair;
using GLOptimizer.Infrastructure.Optimization;
using GLOptimizer.Infrastructure.Logging;
using GLOptimizer.Infrastructure.Navigation;
using GLOptimizer.Infrastructure.Launch;
using GLOptimizer.Infrastructure.Network;
using GLOptimizer.Infrastructure.Settings;
using GLOptimizer.Infrastructure.Startup;
using GLOptimizer.Infrastructure.Updates;
using GLOptimizer.Infrastructure.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GLOptimizer.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGlOptimizerInfrastructure(this IServiceCollection services, string localAppData)
    {
        ArgumentNullException.ThrowIfNull(services);
        var locations = new AppDataLocations(localAppData);
        Directory.CreateDirectory(locations.Root);
        Directory.CreateDirectory(locations.LogsDirectory);

        services.AddSingleton(locations);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<ILogStore, FileLogStore>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IBackupService, FileBackupService>();
        services.AddSingleton<IRepairStateStore, JsonRepairStateStore>();
        services.AddSingleton<ILaunchJournalStore, JsonLaunchJournalStore>();
        services.AddSingleton<IStartupRegistration, WindowsStartupRegistration>();
        services.AddSingleton<IUpdateService, NotImplementedUpdateService>();
        services.AddSingleton<INetworkStatus, LiveNetworkStatus>();
        services.AddSingleton<INetworkProbe, TcpConnectProbe>();
        services.AddSingleton<INetworkDiagnostics, NetworkDiagnosticsService>();
        services.AddSingleton<IConfigFileWriter, AtomicConfigFileWriter>();
        services.AddSingleton<IOptimizationRecordStore, JsonOptimizationRecordStore>();
        services.AddSingleton<IHostOptimizationProbe, NotImplementedHostOptimizationProbe>();
        services.AddSingleton<IBenchmarkService, NotImplementedBenchmarkService>();
        services.AddSingleton<OptimizationProfileSelection>();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>();
        });

        return services;
    }
}

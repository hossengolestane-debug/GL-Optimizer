using GLOptimizer.App.Services;
using GLOptimizer.App.ViewModels;
using GLOptimizer.App.Views;
using GLOptimizer.Core;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Launch;
using GLOptimizer.Core.Logging;
using GLOptimizer.GameLoop;
using GLOptimizer.Infrastructure.DependencyInjection;
using GLOptimizer.Monitoring;
using Microsoft.Extensions.DependencyInjection;

namespace GLOptimizer.App.Composition;

public static class AppHost
{
    public static ServiceProvider Build()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var services = new ServiceCollection();
        services.AddGlOptimizerInfrastructure(localAppData);
        services.AddGameLoopModule();
        services.AddMonitoringModule();
        services.AddSingleton<IFolderOpener, ExplorerFolderOpener>();
        services.AddSingleton<IToastCenter, ToastCenter>();
        services.AddSingleton<IUserConfirmation, MessageBoxConfirmation>();
        services.AddSingleton<PageViewModelFactory>();
        services.AddSingleton<IPageViewModelFactory>(provider => provider.GetRequiredService<PageViewModelFactory>());
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<TrayHost>();
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    public static void Start(ServiceProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        var settings = provider.GetRequiredService<ISettingsStore>();
        var loaded = settings.Load();
        var log = provider.GetRequiredService<ILogStore>();
        log.ApplyPolicy(settings.Current);
        if (!loaded.Succeeded)
        {
            log.Write(LogSeverity.Warning, "Settings", loaded.Error ?? "Settings could not be loaded.");
        }

        log.Write(
            LogSeverity.Information,
            "App",
            $"{BuildInfo.ProductName} {BuildInfo.PhaseName} ({BuildInfo.Version}) started.");
        Warn(log, "Repair", provider.GetRequiredService<IRepairStateStore>().LastProblemAfterLoad());
        Warn(log, "Launch", provider.GetRequiredService<ILaunchJournalStore>().LastProblemAfterLoad());
        var optimization = provider.GetRequiredService<IOptimizationRecordStore>();
        _ = optimization.Read();
        Warn(log, "Optimize", optimization.LastProblem);
        if (!SmokeTest.Active)
        {
            provider.GetRequiredService<LaunchSessionWatcher>().Start();
        }
    }

    private static void Warn(ILogStore log, string category, string? problem)
    {
        if (!string.IsNullOrWhiteSpace(problem))
        {
            log.Write(LogSeverity.Warning, category, problem);
        }
    }
}

internal static class StoreWarnings
{
    public static string? LastProblemAfterLoad(this IRepairStateStore store)
    {
        _ = store.Load();
        return store.LastProblem;
    }

    public static string? LastProblemAfterLoad(this ILaunchJournalStore store)
    {
        _ = store.Load();
        return store.LastProblem;
    }
}

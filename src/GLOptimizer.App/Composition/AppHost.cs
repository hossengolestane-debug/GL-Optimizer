using GLOptimizer.App.Services;
using GLOptimizer.App.ViewModels;
using GLOptimizer.App.Views;
using GLOptimizer.Core;
using GLOptimizer.Core.Abstractions;
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
        services.AddSingleton<IUserConfirmation, MessageBoxConfirmation>();
        services.AddSingleton<IPageViewModelFactory, PageViewModelFactory>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
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
    }
}

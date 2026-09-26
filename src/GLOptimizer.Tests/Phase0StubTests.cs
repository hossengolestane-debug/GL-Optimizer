using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Optimization;
using GLOptimizer.Core.Results;
using GLOptimizer.GameLoop;
using GLOptimizer.Infrastructure.DependencyInjection;
using GLOptimizer.Monitoring;
using Microsoft.Extensions.DependencyInjection;

namespace GLOptimizer.Tests;

public class Phase0StubTests
{
    [Fact]
    public async Task Detection_is_read_only_and_remaining_stubs_create_no_files()
    {
        using var temp = new TempAppData();
        var services = new ServiceCollection();
        services.AddGlOptimizerInfrastructure(temp.LocalAppData);
        services.AddGameLoopModule();
        services.AddMonitoringModule();
        using var provider = services.BuildServiceProvider();

        var before = Snapshot(temp.Root);

        var hardware = await provider.GetRequiredService<IHardwareService>().GetReportAsync();
        var frames = provider.GetRequiredService<IFrameMetricsProvider>().TryGetLatest();
        var gameLoop = await provider.GetRequiredService<IGameLoopDetector>().DetectAsync();
        var installPaths = gameLoop.Value?.Installations
            .Select(installation => installation.InstallPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToArray() ?? [];
        var config = await provider.GetRequiredService<IGameLoopConfigDiscovery>().DiscoverAsync(installPaths);
        var market = await provider.GetRequiredService<IAppMarketDiagnostics>().ScanAsync(checkOfficialVersion: false);
        var backups = provider.GetRequiredService<IBackupService>();
        var listed = backups.List();
        var optimization = provider.GetRequiredService<IOptimizationService>();
        var analysis = await optimization.AnalyzeAsync(OptimizationProfile.Balanced);

        Assert.Equal(OperationStatus.Success, hardware.Status);
        Assert.NotNull(hardware.Value);
        Assert.False(string.IsNullOrWhiteSpace(hardware.Value.Architecture));
        Assert.Equal(OperationStatus.Success, gameLoop.Status);
        Assert.NotNull(gameLoop.Value);
        Assert.Equal(OperationStatus.Success, config.Status);
        Assert.NotNull(config.Value);
        AssertNotImplemented(frames);
        Assert.Equal(OperationStatus.Success, market.Status);
        Assert.NotNull(market.Value);
        Assert.False(market.Value.OfficialRequested);
        Assert.Null(market.Value.OfficialVersion);
        Assert.Equal(OperationStatus.Success, listed.Status);
        Assert.NotNull(listed.Value);
        Assert.Equal(OperationStatus.Success, analysis.Status);
        Assert.NotNull(analysis.Value);
        Assert.Null(frames.Value);
        Assert.Equal(before, Snapshot(temp.Root));
    }

    private static HashSet<string> Snapshot(string root)
    {
        if (!Directory.Exists(root))
        {
            return [];
        }

        return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => path.Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static void AssertNotImplemented<T>(OperationResult<T> result)
    {
        Assert.Equal(OperationStatus.NotImplemented, result.Status);
        Assert.False(result.Succeeded);
        Assert.Null(result.Value);
    }
}

using GLOptimizer.Core.Abstractions;
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
        var market = provider.GetRequiredService<IAppMarketDiagnostics>().Check();
        var backups = provider.GetRequiredService<IBackupService>();
        var listed = backups.List();
        var created = backups.Create("manual");
        var optimization = provider.GetRequiredService<IOptimizationService>();
        var actions = optimization.ListActions();
        var applied = optimization.Apply("noop");

        Assert.Equal(OperationStatus.Success, hardware.Status);
        Assert.NotNull(hardware.Value);
        Assert.False(string.IsNullOrWhiteSpace(hardware.Value.Architecture));
        Assert.Equal(OperationStatus.Success, gameLoop.Status);
        Assert.NotNull(gameLoop.Value);
        AssertNotImplemented(frames);
        AssertNotImplemented(market);
        AssertNotImplemented(listed);
        AssertNotImplemented(actions);
        Assert.Equal(OperationStatus.NotImplemented, created.Status);
        Assert.Equal(OperationStatus.NotImplemented, applied.Status);
        Assert.Null(frames.Value);
        Assert.Null(market.Value);
        Assert.Null(listed.Value);
        Assert.Null(actions.Value);
        Assert.Equal(before, Snapshot(temp.Root));
    }

    [Fact]
    public void Backup_and_optimization_stubs_reject_blank_ids()
    {
        var backups = new GLOptimizer.Infrastructure.Stubs.NotImplementedBackupService();
        var optimization = new GLOptimizer.Infrastructure.Stubs.NotImplementedOptimizationService();

        Assert.Throws<ArgumentException>(() => backups.Create(" "));
        Assert.Throws<ArgumentException>(() => backups.Restore(""));
        Assert.Throws<ArgumentException>(() => optimization.Apply(" "));
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

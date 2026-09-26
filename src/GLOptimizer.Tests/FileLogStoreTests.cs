using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Settings;
using GLOptimizer.Infrastructure;
using GLOptimizer.Infrastructure.DependencyInjection;
using GLOptimizer.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GLOptimizer.Tests;

public class FileLogStoreTests
{
    [Fact]
    public void Writes_filter_and_read_back()
    {
        using var temp = new TempAppData();
        using var store = CreateStore(temp, out var clock);
        store.ApplyPolicy(new AppSettings { MinimumLogLevel = LogSeverity.Warning });
        clock.UtcNow = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

        store.Write(LogSeverity.Information, "App", "hidden");
        store.Write(LogSeverity.Error, "App", "visible", new InvalidOperationException("boom"));

        var recent = store.GetRecent();
        var fromDisk = store.ReadActiveLog();

        Assert.Single(recent);
        Assert.Equal("visible", recent[0].Message);
        Assert.Contains("boom", recent[0].Exception, StringComparison.Ordinal);
        Assert.Equal(recent[0].Message, Assert.Single(fromDisk).Message);
    }

    [Fact]
    public void Rotates_when_the_active_file_exceeds_the_limit()
    {
        using var temp = new TempAppData();
        using var store = CreateStore(temp, out var clock);
        store.ApplyPolicy(new AppSettings
        {
            MinimumLogLevel = LogSeverity.Information,
            MaxLogFileBytes = 64 * 1024
        });
        var payload = new string('x', 5000);
        for (var i = 0; i < 20; i++)
        {
            clock.UtcNow = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero).AddMilliseconds(i);
            store.Write(LogSeverity.Information, "Test", payload);
        }

        store.Flush();
        var files = Directory.GetFiles(Path.Combine(temp.Root, "Logs"), "gloptimizer*.log");
        Assert.True(files.Length > 1, "Expected the active log to rotate into an archive.");
    }

    [Fact]
    public void Purge_removes_old_archives_and_keeps_the_active_log()
    {
        using var temp = new TempAppData();
        using var store = CreateStore(temp, out var clock);
        var logs = Path.Combine(temp.Root, "Logs");
        Directory.CreateDirectory(logs);
        var oldName = LogArchiveNames.Create(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var oldPath = Path.Combine(logs, oldName);
        File.WriteAllText(oldPath, "old");
        clock.UtcNow = new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero);
        store.Write(LogSeverity.Information, "App", "current");

        store.ApplyPolicy(new AppSettings { LogRetentionDays = 14, MinimumLogLevel = LogSeverity.Information });

        Assert.False(File.Exists(oldPath));
        Assert.True(File.Exists(Path.Combine(logs, "gloptimizer.log")));
    }

    [Fact]
    public void Microsoft_logger_reaches_the_file()
    {
        using var temp = new TempAppData();
        var services = new ServiceCollection();
        services.AddGlOptimizerInfrastructure(temp.LocalAppData);
        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<ILogStore>();
        store.ApplyPolicy(new AppSettings { MinimumLogLevel = LogSeverity.Information });

        var logger = provider.GetRequiredService<ILogger<FileLogStoreTests>>();
        logger.LogWarning("hello from the logger");

        Assert.Contains(store.ReadActiveLog(), entry => entry.Message.Contains("hello from the logger", StringComparison.Ordinal));
    }

    private static FileLogStore CreateStore(TempAppData temp, out ManualClock clock)
    {
        clock = new ManualClock();
        var locations = new AppDataLocations(temp.LocalAppData);
        Directory.CreateDirectory(locations.LogsDirectory);
        return new FileLogStore(clock, locations);
    }

    private sealed class ManualClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 26, 0, 0, 0, TimeSpan.Zero);
    }
}

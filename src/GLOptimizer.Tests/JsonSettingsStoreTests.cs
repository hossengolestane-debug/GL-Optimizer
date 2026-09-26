using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Results;
using GLOptimizer.Core.Settings;
using GLOptimizer.Infrastructure;
using GLOptimizer.Infrastructure.Settings;

namespace GLOptimizer.Tests;

public class JsonSettingsStoreTests
{
    [Fact]
    public void Missing_file_is_created_with_defaults()
    {
        using var temp = new TempAppData();
        var store = new JsonSettingsStore(new AppDataLocations(temp.LocalAppData));

        var loaded = store.Load();

        Assert.Equal(OperationStatus.Success, loaded.Status);
        Assert.False(store.Current.SidebarCollapsed);
        Assert.Equal(LogSeverity.Information, store.Current.MinimumLogLevel);
        Assert.True(File.Exists(Path.Combine(temp.Root, "settings.json")));
        Assert.False(File.Exists(Path.Combine(temp.Root, "settings.json.tmp")));
    }

    [Fact]
    public void Roundtrip_persists_normalized_values()
    {
        using var temp = new TempAppData();
        var store = new JsonSettingsStore(new AppDataLocations(temp.LocalAppData));
        store.Load();

        var saved = store.Save(new AppSettings
        {
            SidebarCollapsed = true,
            MinimumLogLevel = LogSeverity.Warning,
            LogRetentionDays = 3,
            MaxLogFileBytes = 5 * 1024 * 1024
        });

        var reloaded = new JsonSettingsStore(new AppDataLocations(temp.LocalAppData));
        var loaded = reloaded.Load();

        Assert.True(saved.Succeeded);
        Assert.True(loaded.Succeeded);
        Assert.True(reloaded.Current.SidebarCollapsed);
        Assert.Equal(LogSeverity.Warning, reloaded.Current.MinimumLogLevel);
        Assert.Equal(3, reloaded.Current.LogRetentionDays);
        Assert.Equal(5 * 1024 * 1024, reloaded.Current.MaxLogFileBytes);
    }

    [Fact]
    public void Corrupt_json_falls_back_to_defaults()
    {
        using var temp = new TempAppData();
        var locations = new AppDataLocations(temp.LocalAppData);
        Directory.CreateDirectory(locations.Root);
        File.WriteAllText(locations.SettingsFile, "{ not json");
        var store = new JsonSettingsStore(locations);

        var loaded = store.Load();

        Assert.Equal(OperationStatus.Failed, loaded.Status);
        Assert.Equal(LogSeverity.Information, store.Current.MinimumLogLevel);
        Assert.False(string.IsNullOrWhiteSpace(loaded.Error));
    }
}

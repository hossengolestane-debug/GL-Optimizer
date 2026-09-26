using System.Text;
using System.Text.Json;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Backup;
using GLOptimizer.Core.Configuration;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Settings;
using GLOptimizer.Infrastructure;
using GLOptimizer.Infrastructure.Backup;

namespace GLOptimizer.Tests;

public class BackupServiceTests
{
    [Fact]
    public async Task Create_copies_allowlisted_files_and_leaves_the_source_unchanged()
    {
        using var temp = new TempTree();
        var install = temp.Dir("install");
        var file = Path.Combine(install, "ui", "config.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var bytes = Encoding.UTF8.GetBytes("VMDPI=240\r\n");
        await File.WriteAllBytesAsync(file, bytes);
        var before = BackupPathRules.Sha256(bytes);
        var written = File.GetLastWriteTimeUtc(file);
        var log = new RecordingLog();
        var service = Service(temp, Snap(install, file), log);

        var created = await service.CreateAsync("Before the match");

        Assert.True(created.Succeeded, created.Error);
        Assert.Equal(before, BackupPathRules.Sha256(await File.ReadAllBytesAsync(file)));
        Assert.Equal(written, File.GetLastWriteTimeUtc(file));
        var manifest = created.Value!.Manifest!;
        Assert.Equal("Before the match", manifest.Reason);
        Assert.Equal("1.2.3", manifest.GameLoopVersion);
        Assert.Equal("240", manifest.Settings.Dpi);
        Assert.Equal("240", manifest.RegistryValues["VMDPI"]);
        Assert.Contains("ForceDirectX", manifest.UnmappedRegistryKeys);
        Assert.Single(manifest.Files);
        Assert.Equal(before, manifest.Files[0].Sha256);
        var stored = Path.Combine(temp.Backups, manifest.Id, manifest.Files[0].StoredName);
        Assert.Equal(before, BackupPathRules.Sha256(await File.ReadAllBytesAsync(stored)));
        Assert.Contains(log.Lines, line => line.Contains("Created backup", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Blank_reason_does_not_create_a_folder()
    {
        using var temp = new TempTree();
        var result = await Service(temp, Snap(temp.Dir("install"), temp.Dir("install"))).CreateAsync("  ");

        Assert.False(result.Succeeded);
        Assert.False(Directory.Exists(temp.Backups));
    }

    [Fact]
    public async Task Restore_requires_confirmation_then_writes_a_pre_restore_backup()
    {
        using var temp = new TempTree();
        var install = temp.Dir("install");
        var file = Path.Combine(install, "config.ini");
        await File.WriteAllTextAsync(file, "alpha");
        var log = new RecordingLog();
        var service = Service(temp, Snap(install, file), log);
        var created = await service.CreateAsync("Manual");
        Assert.True(created.Succeeded, created.Error);
        await File.WriteAllTextAsync(file, "bravo");

        var denied = await service.RestoreAsync(created.Value!.Id, confirmed: false);
        Assert.False(denied.Succeeded);
        Assert.Equal("bravo", await File.ReadAllTextAsync(file));

        var preview = await service.PreviewRestoreAsync(created.Value.Id);
        Assert.True(preview.Value!.CanRestore);
        Assert.Equal(RestoreDisposition.Replace, preview.Value.Files[0].Disposition);
        Assert.NotEqual(preview.Value.Files[0].CurrentSha256, preview.Value.Files[0].BackupSha256);

        var restored = await service.RestoreAsync(created.Value.Id, confirmed: true);
        Assert.True(restored.Succeeded, restored.Error);
        Assert.Equal("alpha", await File.ReadAllTextAsync(file));
        Assert.False(string.IsNullOrWhiteSpace(restored.Value!.PreRestoreBackupId));
        var pre = service.Get(restored.Value.PreRestoreBackupId!);
        Assert.False(pre.Value!.Damaged);
        var preFile = Path.Combine(temp.Backups, pre.Value.Manifest!.Id, pre.Value.Manifest.Files[0].StoredName);
        Assert.Equal(BackupPathRules.Sha256(Encoding.UTF8.GetBytes("bravo")), BackupPathRules.Sha256(await File.ReadAllBytesAsync(preFile)));
        Assert.Contains(log.Lines, line => line.Contains("Restored", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Running_gameloop_blocks_restore()
    {
        using var temp = new TempTree();
        var install = temp.Dir("install");
        var file = Path.Combine(install, "config.ini");
        await File.WriteAllTextAsync(file, "alpha");
        var source = new FixedSource(Snap(install, file));
        var service = Service(temp, source.Snapshot, source: source);
        var created = await service.CreateAsync("Manual");
        await File.WriteAllTextAsync(file, "bravo");
        source.Snapshot = Snap(install, file, running: true);

        var preview = await service.PreviewRestoreAsync(created.Value!.Id);
        Assert.False(preview.Value!.CanRestore);
        Assert.Contains("running", preview.Value.Warning, StringComparison.OrdinalIgnoreCase);
        var restored = await service.RestoreAsync(created.Value.Id, confirmed: true);
        Assert.False(restored.Succeeded);
        Assert.Equal("bravo", await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task Traversal_outside_paths_and_symlinks_are_refused()
    {
        using var temp = new TempTree();
        var install = temp.Dir("install");
        var file = Path.Combine(install, "config.ini");
        await File.WriteAllTextAsync(file, "alpha");
        var outside = temp.Dir("outside");
        var outsideFile = Path.Combine(outside, "other.ini");
        await File.WriteAllTextAsync(outsideFile, "keep");
        var service = Service(temp, Snap(install, file));
        var created = await service.CreateAsync("Manual");
        var id = created.Value!.Id;
        var manifestPath = Path.Combine(temp.Backups, id, "manifest.json");

        await Rewrite(manifestPath, manifest => manifest.Files[0].StoredName = "../escape.txt");
        var traversal = await service.PreviewRestoreAsync(id);
        Assert.False(traversal.Value!.CanRestore);
        Assert.False(File.Exists(Path.Combine(temp.Backups, "escape.txt")));
        Assert.False((await service.RestoreAsync(id, true)).Succeeded);
        Assert.False(File.Exists(Path.Combine(temp.Backups, "escape.txt")));

        await Rewrite(manifestPath, manifest =>
        {
            manifest.Files[0].StoredName = "01-config.ini";
            manifest.Files[0].OriginalPath = outsideFile;
        });
        var outsidePreview = await service.PreviewRestoreAsync(id);
        Assert.Equal(RestoreDisposition.Skip, outsidePreview.Value!.Files[0].Disposition);
        Assert.False((await service.RestoreAsync(id, true)).Succeeded);
        Assert.Equal("keep", await File.ReadAllTextAsync(outsideFile));

        await Rewrite(manifestPath, manifest => manifest.Files[0].OriginalPath = file);
        File.Delete(file);
        var linked = Path.Combine(outside, "linked.ini");
        await File.WriteAllTextAsync(linked, "linked");
        File.CreateSymbolicLink(file, linked);
        var linkPreview = await service.PreviewRestoreAsync(id);
        Assert.Equal(RestoreDisposition.Skip, linkPreview.Value!.Files[0].Disposition);
        Assert.Contains("link", linkPreview.Value.Files[0].Detail, StringComparison.OrdinalIgnoreCase);
        Assert.False((await service.RestoreAsync(id, true)).Succeeded);
        Assert.Equal("linked", await File.ReadAllTextAsync(linked));
    }

    [Fact]
    public async Task Tampered_backup_bytes_are_not_restored()
    {
        using var temp = new TempTree();
        var install = temp.Dir("install");
        var file = Path.Combine(install, "config.ini");
        await File.WriteAllTextAsync(file, "alpha");
        var service = Service(temp, Snap(install, file));
        var created = await service.CreateAsync("Manual");
        var stored = Path.Combine(temp.Backups, created.Value!.Id, created.Value.Manifest!.Files[0].StoredName);
        var bytes = await File.ReadAllBytesAsync(stored);
        bytes[0] ^= 0xFF;
        await File.WriteAllBytesAsync(stored, bytes);
        await File.WriteAllTextAsync(file, "bravo");

        var preview = await service.PreviewRestoreAsync(created.Value.Id);
        Assert.Equal(RestoreDisposition.Skip, preview.Value!.Files[0].Disposition);
        Assert.False((await service.RestoreAsync(created.Value.Id, true)).Succeeded);
        Assert.Equal("bravo", await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task Corrupt_and_missing_manifests_are_damaged_and_do_not_throw()
    {
        using var temp = new TempTree();
        Directory.CreateDirectory(Path.Combine(temp.Backups, "damaged1"));
        await File.WriteAllTextAsync(Path.Combine(temp.Backups, "damaged1", "manifest.json"), "{");
        Directory.CreateDirectory(Path.Combine(temp.Backups, "missing1"));
        var service = Service(temp, Snap(temp.Dir("install"), temp.Dir("install")));

        var listed = service.List();
        Assert.True(listed.Succeeded);
        Assert.Equal(2, listed.Value!.Count);
        Assert.All(listed.Value, record => Assert.True(record.Damaged));
        Assert.Contains("could not be read", service.Get("damaged1").Value!.Problem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("missing", service.Get("missing1").Value!.Problem, StringComparison.OrdinalIgnoreCase);
        Assert.False((await service.RestoreAsync("damaged1", true)).Succeeded);
        Assert.False((await service.RestoreAsync("missing1", true)).Succeeded);
    }

    [Fact]
    public async Task Delete_stays_inside_the_backups_root()
    {
        using var temp = new TempTree();
        var install = temp.Dir("install");
        var file = Path.Combine(install, "config.ini");
        await File.WriteAllTextAsync(file, "alpha");
        var keep = Path.Combine(temp.Root, "keep.txt");
        await File.WriteAllTextAsync(keep, "keep");
        var outside = temp.Dir("outside");
        await File.WriteAllTextAsync(Path.Combine(outside, "keep.txt"), "keep");
        Directory.CreateDirectory(temp.Backups);
        Directory.CreateSymbolicLink(Path.Combine(temp.Backups, "linkbak1"), outside);
        var log = new RecordingLog();
        var service = Service(temp, Snap(install, file), log);
        var created = await service.CreateAsync("Manual");
        var id = created.Value!.Id;

        Assert.False((await service.DeleteAsync(id, confirmed: false)).Succeeded);
        Assert.True(Directory.Exists(Path.Combine(temp.Backups, id)));
        Assert.False((await service.DeleteAsync("../" + Path.GetFileName(keep), confirmed: true)).Succeeded);
        Assert.False((await service.DeleteAsync("..", confirmed: true)).Succeeded);
        Assert.False((await service.DeleteAsync("linkbak1", confirmed: true)).Succeeded);
        Assert.Equal("keep", await File.ReadAllTextAsync(keep));
        Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(outside, "keep.txt")));

        var deleted = await service.DeleteAsync(id, confirmed: true);
        Assert.True(deleted.Succeeded, deleted.Error);
        Assert.False(Directory.Exists(Path.Combine(temp.Backups, id)));
        Assert.True(Directory.Exists(temp.Backups));
        Assert.Contains(log.Lines, line => line.Contains("Deleted backup", StringComparison.Ordinal));
    }

    [Fact]
    public async Task User_config_location_can_be_backed_up_when_it_is_allowlisted()
    {
        using var temp = new TempTree();
        var userDir = temp.Dir("user");
        var file = Path.Combine(userDir, "config.ini");
        await File.WriteAllTextAsync(file, "user");
        var snapshot = Snap(temp.Dir("install"), file);
        snapshot = new BackupSourceSnapshot
        {
            InstallRoots = snapshot.InstallRoots,
            UserDirectories = [userDir],
            Files = [new BackupSourceFile { Path = file }],
            Settings = snapshot.Settings,
            GameLoopVersion = snapshot.GameLoopVersion,
            RegistryValues = snapshot.RegistryValues
        };
        var service = Service(temp, snapshot);
        var created = await service.CreateAsync("User file");
        Assert.True(created.Succeeded, created.Error);
        await File.WriteAllTextAsync(file, "changed");
        var restored = await service.RestoreAsync(created.Value!.Id, confirmed: true);
        Assert.True(restored.Succeeded, restored.Error);
        Assert.Equal("user", await File.ReadAllTextAsync(file));
    }

    private static async Task Rewrite(string manifestPath, Action<BackupManifest> edit)
    {
        var manifest = JsonSerializer.Deserialize<BackupManifest>(await File.ReadAllTextAsync(manifestPath))!;
        edit(manifest);
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));
    }

    private static FileBackupService Service(TempTree temp, BackupSourceSnapshot snapshot, RecordingLog? log = null, FixedSource? source = null) =>
        new(source ?? new FixedSource(snapshot), new FixedClock(), log ?? new RecordingLog(), new AppDataLocations(temp.AppData));

    private static BackupSourceSnapshot Snap(string install, string file, bool running = false) => new()
    {
        InstallRoots = [Path.GetFullPath(install)],
        Files = [new BackupSourceFile { Path = Path.GetFullPath(file) }],
        Settings = new GameLoopSettings { Dpi = "240", VSync = "Off" },
        GameLoopVersion = "1.2.3",
        GameLoopRunning = running,
        RegistryValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["VMDPI"] = "240" },
        UnmappedRegistryKeys = ["ForceDirectX"]
    };

    private sealed class TempTree : IDisposable
    {
        public TempTree()
        {
            Root = Path.Combine(Path.GetTempPath(), "glopt-bak-" + Guid.NewGuid().ToString("N"));
            AppData = Path.Combine(Root, "appdata");
            Directory.CreateDirectory(AppData);
            Backups = Path.Combine(AppData, "GLOptimizer", "Backups");
        }

        public string Root { get; }

        public string AppData { get; }

        public string Backups { get; }

        public string Dir(string name)
        {
            var path = Path.Combine(Root, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (Exception)
            {
                // The temp tree is best-effort.
            }
        }
    }

    private sealed class FixedSource : IBackupSource
    {
        public FixedSource(BackupSourceSnapshot snapshot) => Snapshot = snapshot;

        public BackupSourceSnapshot Snapshot { get; set; }

        public Task<BackupSourceSnapshot> CaptureAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Snapshot);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new DateTimeOffset(2026, 9, 26, 17, 0, 0, TimeSpan.Zero);
    }

    private sealed class RecordingLog : ILogStore
    {
        public List<string> Lines { get; } = [];

        public string LogDirectory => string.Empty;

        public string ActiveLogFilePath => string.Empty;

        public LogSeverity MinimumLevel => LogSeverity.Information;

        public string? LastError => null;

        public void ApplyPolicy(AppSettings settings)
        {
        }

        public void Write(LogSeverity severity, string category, string message, Exception? exception = null) =>
            Lines.Add(category + " " + message);

        public IReadOnlyList<LogEntry> GetRecent(int count = 200) => [];

        public IReadOnlyList<LogEntry> ReadActiveLog(int maxLines = 500) => [];

        public void Flush()
        {
        }
    }
}

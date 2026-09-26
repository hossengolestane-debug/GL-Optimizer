using System.Text;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Backup;
using GLOptimizer.Core.Configuration;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Optimization;
using GLOptimizer.Core.Results;
using GLOptimizer.Core.Settings;
using GLOptimizer.GameLoop;
using GLOptimizer.Infrastructure;
using GLOptimizer.Infrastructure.Backup;
using GLOptimizer.Infrastructure.Optimization;

namespace GLOptimizer.Tests;

public class OptimizationTests
{
    [Fact]
    public void Tier_uses_cores_memory_and_vram_and_unknown_when_key_data_is_missing()
    {
        Assert.Equal(HardwareTier.Unknown, HardwareTierClassifier.Classify(new HardwareReport()));
        Assert.Equal(HardwareTier.Unknown, HardwareTierClassifier.Classify(new HardwareReport { LogicalCores = 8 }));
        Assert.Equal(HardwareTier.Low, HardwareTierClassifier.Classify(Hardware(4, 32, null)));
        Assert.Equal(HardwareTier.Low, HardwareTierClassifier.Classify(Hardware(8, 4, null)));
        Assert.Equal(HardwareTier.Mid, HardwareTierClassifier.Classify(Hardware(6, 12, null)));
        Assert.Equal(HardwareTier.High, HardwareTierClassifier.Classify(Hardware(8, 16, null)));
        Assert.Equal(HardwareTier.Mid, HardwareTierClassifier.Classify(Hardware(8, 16, 1)));
    }

    [Fact]
    public void Headroom_never_assigns_every_core_or_almost_all_memory()
    {
        Assert.Null(HardwareTierClassifier.CpuHeadroom(1));
        Assert.Equal(1, HardwareTierClassifier.CpuHeadroom(2));
        Assert.Equal(2, HardwareTierClassifier.CpuHeadroom(4));
        Assert.Equal(4, HardwareTierClassifier.CpuHeadroom(8));
        Assert.Null(HardwareTierClassifier.MemoryHeadroomMb(4L * HardwareTierClassifier.Gibibyte));
        Assert.Equal(4096, HardwareTierClassifier.MemoryHeadroomMb(8L * HardwareTierClassifier.Gibibyte));
        Assert.Equal(8192, HardwareTierClassifier.MemoryHeadroomMb(16L * HardwareTierClassifier.Gibibyte));

        var eightCores = Recommend(OptimizationProfile.Performance, Hardware(8, 16, null), Located("CpuAllocation", "8", ("VMCpuCount", "8")));
        Assert.Equal("4", Only(eightCores, "CpuAllocation").RecommendedValue);

        var hungry = Recommend(OptimizationProfile.Balanced, Hardware(8, 16, null), Located("MemoryAllocation", "16384 MB", ("VMMemorySizeInMB", "16384")));
        Assert.Equal("8192 MB", Only(hungry, "MemoryAllocation").RecommendedValue);
    }

    [Fact]
    public void Profiles_choose_resolution_and_unknown_hardware_stays_conservative()
    {
        var low = Recommend(OptimizationProfile.Performance, Hardware(4, 8, null), Located("Resolution", "1920×1080", ("Resolution", "1920x1080")));
        Assert.Equal("1280×720", Only(low, "Resolution").RecommendedValue);
        Assert.Equal(RecommendationStatus.Applicable, Only(low, "Resolution").Status);

        var mid = Recommend(OptimizationProfile.Balanced, Hardware(6, 12, null), Located("Resolution", "1280×720", ("Resolution", "1280x720")));
        Assert.Equal("1920×1080", Only(mid, "Resolution").RecommendedValue);

        var high = Recommend(OptimizationProfile.Quality, Hardware(8, 16, 8), Located("Resolution", "1920×1080", ("Resolution", "1920x1080")));
        Assert.Equal("2560×1440", Only(high, "Resolution").RecommendedValue);

        var unknown = Recommend(OptimizationProfile.Performance, new HardwareReport(), Located("Resolution", "2560×1440", ("Resolution", "2560x1440")));
        Assert.Equal(HardwareTier.Unknown, HardwareTierClassifier.Classify(new HardwareReport()));
        Assert.Equal("1280×720", Only(unknown, "Resolution").RecommendedValue);

        var already = Recommend(OptimizationProfile.Performance, new HardwareReport(), Located("Resolution", "1280×720", ("Resolution", "1280x720")));
        Assert.Equal(RecommendationStatus.AlreadyOptimal, Only(already, "Resolution").Status);

        var fps = Recommend(OptimizationProfile.Performance, new HardwareReport(), Located("FpsTarget", "30", ("FPSLevel", "30")));
        Assert.Equal(RecommendationStatus.AlreadyOptimal, Only(fps, "FpsTarget").Status);
    }

    [Fact]
    public void Renderer_is_compatibility_only_and_registry_is_not_applied()
    {
        var plus = Recommend(OptimizationProfile.Balanced, Hardware(8, 16, null), Located("Renderer", "OpenGL+", ("Renderer", "OpenGL+")));
        var renderer = Only(plus, "Renderer");
        Assert.Equal("OpenGL", renderer.RecommendedValue);
        Assert.Contains("Compatibility-based", renderer.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("FPS", renderer.Reason, StringComparison.Ordinal);

        var registry = OptimizationRules.Build(HardwareTier.High, OptimizationProfile.Performance, Hardware(8, 16, null),
        [
            new LocatedSetting
            {
                Name = "Dpi",
                Path = @"HKCU\Software\Tencent\MobileGamePC",
                Format = "registry",
                Normalized = "480",
                FromRegistry = true,
                Valid = true,
                Keys = [new RawSettingKey { Name = "VMDPI", Raw = "480" }]
            }
        ]);
        Assert.Equal(RecommendationStatus.NotSupported, Only(registry, "Dpi").Status);

        var custom = Recommend(OptimizationProfile.Custom, Hardware(8, 16, null), Located("Dpi", "480", ("VMDPI", "480")));
        Assert.Equal(RecommendationStatus.Skipped, Only(custom, "Dpi").Status);
        Assert.Empty(Only(custom, "Dpi").Edits);
    }

    [Theory]
    [InlineData("ini")]
    [InlineData("json")]
    [InlineData("xml")]
    public void Edits_preserve_unrelated_bytes_and_never_add_a_key(string format)
    {
        var (text, edit) = Sample(format);
        Assert.True(ConfigTextEditor.TryApply(text, format, [edit], out var updated, out var error), error);
        Assert.Equal(text.Replace("240", "160", StringComparison.Ordinal), updated);
        Assert.Contains("keep", updated, StringComparison.Ordinal);

        var missing = new OptimizationEdit
        {
            Setting = edit.Setting,
            Path = edit.Path,
            Format = edit.Format,
            Key = "NotPresent",
            CurrentRaw = edit.CurrentRaw,
            NewRaw = edit.NewRaw
        };
        Assert.False(ConfigTextEditor.TryApply(text, format, [missing], out var unchanged, out _));
        Assert.Equal(text, unchanged);
    }

    [Fact]
    public void Utf8_bom_and_line_endings_survive_an_ini_edit()
    {
        var text = "; keep\r\nVMDPI=240\r\nOther=keep\r\n";
        var bom = ConfigFileEncoding.Encode(text, Encoding.UTF8, bom: true);
        var decoded = ConfigFileEncoding.Decode(bom);
        Assert.True(ConfigTextEditor.TryApply(decoded.Text, "ini",
        [
            new OptimizationEdit
            {
                Setting = "Dpi",
                Path = "config.ini",
                Format = "ini",
                Key = "VMDPI",
                CurrentRaw = "240",
                NewRaw = "160"
            }
        ], out var updated, out var error), error);
        var encoded = ConfigFileEncoding.Encode(updated, decoded.Encoding, decoded.Bom);
        Assert.Equal(0xEF, encoded[0]);
        Assert.Equal(0xBB, encoded[1]);
        Assert.Equal(0xBF, encoded[2]);
        Assert.Contains("\r\n", updated, StringComparison.Ordinal);
        Assert.Contains("Other=keep", updated, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Apply_matches_the_preview_and_undo_restores_the_original()
    {
        using var temp = new TempTree();
        var install = temp.Dir("install");
        var file = Path.Combine(install, "config.ini");
        var original = "; keep\r\nVMDPI=240\r\nOther=keep\r\n";
        await File.WriteAllTextAsync(file, original);
        var engine = Engine(temp, install, file, running: false, corrupt: false);

        var preview = await engine.PreviewAsync(OptimizationProfile.Performance);
        Assert.True(preview.Succeeded, preview.Error);
        Assert.Equal(original, await File.ReadAllTextAsync(file));
        var dpi = Assert.Single(preview.Value!.Edits, edit => edit.Key == "VMDPI");
        Assert.Equal("240", dpi.CurrentRaw);
        Assert.Equal("160", dpi.NewRaw);

        var denied = await engine.ApplyAsync(OptimizationProfile.Performance, confirmed: false);
        Assert.False(denied.Succeeded);
        Assert.Equal(original, await File.ReadAllTextAsync(file));

        var applied = await engine.ApplyAsync(OptimizationProfile.Performance, confirmed: true);
        Assert.True(applied.Succeeded, applied.Error);
        Assert.Equal(original.Replace("240", "160", StringComparison.Ordinal), await File.ReadAllTextAsync(file));
        Assert.Contains("GameLoop configuration updated", applied.Value!.ResultText, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(applied.Value.BackupId));
        var recordPath = Path.Combine(new AppDataLocations(temp.AppData).Root, "last-optimization.json");
        var saved = System.Text.Json.JsonSerializer.Deserialize<OptimizationUndoRecord>(
            await File.ReadAllTextAsync(recordPath),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(saved);
        Assert.False(saved!.InProgress);
        Assert.Equal(applied.Value.BackupId, saved.BackupId);

        var undone = await engine.UndoLastAsync(confirmed: true);
        Assert.True(undone.Succeeded, undone.Error);
        Assert.Equal(original, await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task Running_gameloop_refuses_the_apply()
    {
        using var temp = new TempTree();
        var install = temp.Dir("install");
        var file = Path.Combine(install, "config.ini");
        var original = "VMDPI=240\r\n";
        await File.WriteAllTextAsync(file, original);
        var engine = Engine(temp, install, file, running: true, corrupt: false);

        var applied = await engine.ApplyAsync(OptimizationProfile.Performance, confirmed: true);
        Assert.False(applied.Succeeded);
        Assert.Contains("running", applied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(original, await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task Validation_failure_restores_the_pre_apply_backup()
    {
        using var temp = new TempTree();
        var install = temp.Dir("install");
        var file = Path.Combine(install, "config.ini");
        var original = "VMDPI=240\r\nOther=keep\r\n";
        await File.WriteAllTextAsync(file, original);
        var engine = Engine(temp, install, file, running: false, corrupt: true);

        var applied = await engine.ApplyAsync(OptimizationProfile.Performance, confirmed: true);
        Assert.False(applied.Succeeded);
        Assert.Contains("restored", applied.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(original, await File.ReadAllTextAsync(file));
        Assert.False(File.Exists(Path.Combine(new AppDataLocations(temp.AppData).Root, "last-optimization.json")));
    }

    private static IReadOnlyList<OptimizationRecommendation> Recommend(OptimizationProfile profile, HardwareReport hardware, params LocatedSetting[] located) =>
        OptimizationRules.Build(HardwareTierClassifier.Classify(hardware), profile, hardware, located);

    private static OptimizationRecommendation Only(IReadOnlyList<OptimizationRecommendation> rows, string setting)
    {
        var match = Assert.Single(rows, row => row.Setting == setting);
        return match;
    }

    private static LocatedSetting Located(string name, string normalized, params (string Key, string Raw)[] keys) => new()
    {
        Name = name,
        Path = OperatingSystem.IsWindows() ? @"C:\GameLoop\config.ini" : "/tmp/GameLoop/config.ini",
        Format = "ini",
        Normalized = normalized,
        Valid = true,
        Keys = keys.Select(key => new RawSettingKey { Name = key.Key, Raw = key.Raw }).ToArray()
    };

    private static HardwareReport Hardware(int cores, int gigabytes, int? vramGb) => new()
    {
        LogicalCores = cores,
        TotalMemoryBytes = gigabytes * HardwareTierClassifier.Gibibyte,
        GpuName = "Test GPU",
        GpuMemoryBytes = vramGb is null ? null : vramGb.Value * HardwareTierClassifier.Gibibyte,
        CpuName = "Test CPU"
    };

    private static (string Text, OptimizationEdit Edit) Sample(string format)
    {
        var edit = new OptimizationEdit
        {
            Setting = "Dpi",
            Path = "config." + format,
            Format = format,
            Key = "VMDPI",
            CurrentRaw = "240",
            NewRaw = "160"
        };
        var text = format switch
        {
            "json" => "{\r\n  \"VMDPI\": 240,\r\n  \"Other\": \"keep\"\r\n}\r\n",
            "xml" => "<config>\r\n  <VMDPI>240</VMDPI>\r\n  <Other>keep</Other>\r\n</config>\r\n",
            _ => "; keep\r\nVMDPI=240\r\nOther=keep\r\n"
        };
        return (text, edit);
    }

    private static OptimizationEngine Engine(TempTree temp, string install, string file, bool running, bool corrupt)
    {
        var snapshot = new BackupSourceSnapshot
        {
            InstallRoots = [Path.GetFullPath(install)],
            Files = [new BackupSourceFile { Path = Path.GetFullPath(file) }],
            Settings = new GameLoopSettings { Dpi = "240" },
            GameLoopVersion = "1.2.3"
        };
        var backups = new FileBackupService(new FixedSource(snapshot), new FixedClock(), new RecordingLog(), new AppDataLocations(temp.AppData));
        return new OptimizationEngine(
            new FixedHardware(),
            new FixedDetector(install, running),
            new FixedDiscovery(install, file),
            new FixedReader(),
            backups,
            new NotImplementedHostOptimizationProbe(),
            corrupt ? new CorruptWriter() : new AtomicConfigFileWriter(),
            new JsonOptimizationRecordStore(new AppDataLocations(temp.AppData)),
            new FixedClock(),
            new RecordingLog());
    }

    private sealed class TempTree : IDisposable
    {
        public TempTree()
        {
            Root = Path.Combine(Path.GetTempPath(), "glopt-opt-" + Guid.NewGuid().ToString("N"));
            AppData = Path.Combine(Root, "appdata");
            Directory.CreateDirectory(AppData);
        }

        public string Root { get; }

        public string AppData { get; }

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

    private sealed class FixedHardware : IHardwareService
    {
        public Task<OperationResult<HardwareReport>> GetReportAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult<HardwareReport>.Success(Hardware(4, 8, null)));
    }

    private sealed class FixedDetector : IGameLoopDetector
    {
        private readonly string _install;
        private readonly bool _running;

        public FixedDetector(string install, bool running)
        {
            _install = install;
            _running = running;
        }

        public Task<OperationResult<GameLoopScan>> DetectAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult<GameLoopScan>.Success(new GameLoopScan
            {
                Installations =
                [
                    new GameLoopInstallation
                    {
                        InstallPath = Path.GetFullPath(_install),
                        Version = "1.2.3",
                        RunStatus = _running ? GameRunStatus.Running : GameRunStatus.Stopped
                    }
                ]
            }));
    }

    private sealed class FixedDiscovery : IGameLoopConfigDiscovery
    {
        private readonly string _install;
        private readonly string _file;

        public FixedDiscovery(string install, string file)
        {
            _install = install;
            _file = file;
        }

        public Task<OperationResult<GameLoopConfigReport>> DiscoverAsync(IReadOnlyList<string> installPaths, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult<GameLoopConfigReport>.Success(new GameLoopConfigReport
            {
                Installs =
                [
                    new InstallConfigReport
                    {
                        InstallPath = Path.GetFullPath(_install),
                        Files =
                        [
                            new ConfigFileRecord
                            {
                                Path = Path.GetFullPath(_file),
                                Kind = ConfigFileKind.EngineSettings,
                                Presence = ConfigPresence.Present,
                                Detail = "Parsed."
                            }
                        ]
                    }
                ]
            }));
    }

    private sealed class FixedReader : IGameLoopConfigReader
    {
        public ConfigProbe ProbeFile(string path, bool readText) => ConfigProbe.Missing();

        public RegistryProbe ReadMobileGamePc() => new();

        public IReadOnlyList<string> KnownUserFiles() => [];
    }

    private sealed class FixedSource : IBackupSource
    {
        private readonly BackupSourceSnapshot _snapshot;

        public FixedSource(BackupSourceSnapshot snapshot) => _snapshot = snapshot;

        public Task<BackupSourceSnapshot> CaptureAsync(CancellationToken cancellationToken = default) => Task.FromResult(_snapshot);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new DateTimeOffset(2026, 9, 26, 18, 0, 0, TimeSpan.Zero);
    }

    private sealed class RecordingLog : ILogStore
    {
        public string LogDirectory => string.Empty;

        public string ActiveLogFilePath => string.Empty;

        public LogSeverity MinimumLevel => LogSeverity.Information;

        public string? LastError => null;

        public void ApplyPolicy(AppSettings settings)
        {
        }

        public void Write(LogSeverity severity, string category, string message, Exception? exception = null)
        {
        }

        public IReadOnlyList<LogEntry> GetRecent(int count = 200) => [];

        public IReadOnlyList<LogEntry> ReadActiveLog(int maxLines = 500) => [];

        public void Flush()
        {
        }
    }

    private sealed class CorruptWriter : IConfigFileWriter
    {
        public OperationResult Write(string path, byte[] contents)
        {
            File.WriteAllBytes(path, "VMDPI=999\r\nOther=changed\r\n"u8.ToArray());
            return OperationResult.Success();
        }
    }
}

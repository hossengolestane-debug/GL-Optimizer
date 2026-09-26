using System.Security.Cryptography;
using System.Text;
using GLOptimizer.Core.Configuration;
using GLOptimizer.GameLoop;

namespace GLOptimizer.Tests;

public class GameLoopConfigTests
{
    [Fact]
    public void Parser_maps_documented_keys_and_leaves_the_rest_unknown()
    {
        const string ini = """
            ; comment
            [Engine]
            VMDPI=240
            VMCpuCount=4
            VMMemorySizeInMB=4096
            VMResWidth=1280
            VMResHeight=720
            VSyncEnabled=0
            FxaaQuality=0
            Renderer=OpenGL+
            com.tencent.tmgp.pubgmhd_FPSLevel=90
            ForceDirectX=3
            QqOpenid=secret-token-value
            """;

        Assert.True(GameLoopSettingsParser.TryReadPairs(ini, ".ini", out var pairs));
        var settings = GameLoopSettingsParser.Parse(pairs);

        Assert.Equal("OpenGL+", settings.Renderer);
        Assert.Equal("1280×720", settings.Resolution);
        Assert.Equal("240", settings.Dpi);
        Assert.Equal("4096 MB", settings.MemoryAllocation);
        Assert.Equal("4", settings.CpuAllocation);
        Assert.Equal("Off", settings.VSync);
        Assert.Equal("0", settings.AntiAliasing);
        Assert.Equal("90", settings.FpsTarget);
    }

    [Fact]
    public void Parser_accepts_json_and_xml_shapes()
    {
        const string json = """{"engine":{"VMDPI":320,"Resolution":"1920x1080","VSync":"On","AntiAliasing":"Ultra"}}""";
        Assert.True(GameLoopSettingsParser.TryReadPairs(json, ".json", out var jsonPairs));
        var fromJson = GameLoopSettingsParser.Parse(jsonPairs);
        Assert.Equal("320", fromJson.Dpi);
        Assert.Equal("1920×1080", fromJson.Resolution);
        Assert.Equal("On", fromJson.VSync);
        Assert.Equal("Ultra", fromJson.AntiAliasing);

        const string xml = """<Config><VMMemorySizeInMB>2048</VMMemorySizeInMB><CpuAllocation>2</CpuAllocation></Config>""";
        Assert.True(GameLoopSettingsParser.TryReadPairs(xml, ".xml", out var xmlPairs));
        var fromXml = GameLoopSettingsParser.Parse(xmlPairs);
        Assert.Equal("2048 MB", fromXml.MemoryAllocation);
        Assert.Equal("2", fromXml.CpuAllocation);
    }

    [Fact]
    public void Unreliable_values_stay_unknown()
    {
        var settings = GameLoopSettingsParser.Parse(
        [
            new ConfigPair("VMCpuCount", "0"),
            new ConfigPair("VMDPI", "5"),
            new ConfigPair("VMResWidth", "1280"),
            new ConfigPair("ForceDirectX", "1"),
            new ConfigPair("EnableGLESv3", "1"),
            new ConfigPair("VMMemorySizeInMB", "aaaaaaaaaaaaaaaaaaaaaaaa"),
            new ConfigPair("QqOpenid", "user@example.com"),
            new ConfigPair("com.tencent.tmgp.pubgmhd_FPSLevel", "90"),
            new ConfigPair("com.activision.callofduty.shooter_FPSLevel", "60")
        ]);

        Assert.Null(settings.Renderer);
        Assert.Null(settings.Resolution);
        Assert.Null(settings.Dpi);
        Assert.Null(settings.MemoryAllocation);
        Assert.Null(settings.CpuAllocation);
        Assert.Null(settings.FpsTarget);
    }

    [Fact]
    public void Conflicting_files_clear_the_setting()
    {
        var first = GameLoopSettingsParser.Parse([new ConfigPair("VMDPI", "240")]);
        var second = GameLoopSettingsParser.Parse([new ConfigPair("VMDPI", "320")]);
        var merged = GameLoopSettingsParser.Merge([first, second]);

        Assert.Null(merged.Dpi);
    }

    [Fact]
    public void Conf_parses_like_ini_and_names_unrecognized_keys()
    {
        const string conf = """
            Renderer=OpenGL
            CustomFlag=1
            """;

        Assert.True(GameLoopSettingsParser.TryReadPairs(conf, ".conf", out var pairs));
        var settings = GameLoopSettingsParser.Parse(pairs);

        Assert.Equal("OpenGL", settings.Renderer);
        Assert.Contains("CustomFlag", GameLoopSettingsParser.UnrecognizedNames(pairs));
    }

    [Fact]
    public async Task Data_root_and_supplemental_registry_stay_read_only()
    {
        var parent = Path.Combine(Path.GetTempPath(), "glopt-data-" + Guid.NewGuid().ToString("N"));
        var install = Path.GetFullPath(Path.Combine(parent, "GameLoop"));
        var data = Path.GetFullPath(Path.Combine(parent, "GameLoopData"));
        var config = Path.GetFullPath(Path.Combine(data, "Component", "GameLoop", "Config.json"));
        var reader = new ScriptedReader();
        reader.Files[config] = new ConfigProbe
        {
            Exists = true,
            Text = "{\"VMDPI\":240,\"CustomSetting\":1}",
            SizeBytes = 32
        };
        reader.Supplemental.Add(new SupplementalRegistry
        {
            Path = @"HKCU\Software\Tencent\Call-of-Duty-Mobile-PCLauncher",
            Found = true,
            ValueNames = ["UserSettingLevel_KEY_NEW_h1794756831"]
        });
        reader.Supplemental.Add(new SupplementalRegistry
        {
            Path = @"HKLM\SOFTWARE\Tencent\GameLoop",
            Found = true,
            ValueNames = ["HyperVState", "VtState"]
        });

        var result = await new GameLoopConfigDiscovery(reader).DiscoverAsync([install, data]);

        Assert.True(result.Succeeded);
        var report = Assert.Single(result.Value!.Installs);
        Assert.Equal(install, report.InstallPath);
        Assert.Equal("240", report.Settings.Dpi);
        Assert.Null(report.Settings.Resolution);
        Assert.Null(result.Value.Notice);
        Assert.Contains(report.Files, file => file.Path == config && file.Detail != null && file.Detail.Contains("CustomSetting", StringComparison.Ordinal));
        Assert.Contains(report.Files, file =>
            file.Path.Contains("Call-of-Duty-Mobile-PCLauncher", StringComparison.Ordinal)
            && file.Presence == ConfigPresence.Present
            && file.Detail != null
            && file.Detail.Contains("UserSettingLevel_KEY_NEW_h1794756831", StringComparison.Ordinal)
            && file.Detail.Contains("read-only", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Files, file =>
            file.Path.StartsWith("HKLM", StringComparison.Ordinal)
            && file.Detail != null
            && file.Detail.Contains("not written", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Invalid_documents_are_rejected()
    {
        Assert.True(GameLoopSettingsParser.TryReadPairs("   ", ".ini", out var empty));
        Assert.Empty(empty);
        Assert.False(GameLoopSettingsParser.TryReadPairs("{", ".json", out _));
        Assert.False(GameLoopSettingsParser.TryReadPairs("<!DOCTYPE config [<!ENTITY x \"x\">]><config>&x;</config>", ".xml", out _));
    }

    [Fact]
    public async Task Discovery_reads_a_fixture_without_changing_it()
    {
        var root = Path.Combine(Path.GetTempPath(), "glopt-cfg-" + Guid.NewGuid().ToString("N"));
        var configPath = Path.Combine(root, "ui", "config.ini");
        var outside = Path.Combine(Path.GetTempPath(), "glopt-cfg-out-" + Guid.NewGuid().ToString("N") + ".ini");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        var bytes = Encoding.UTF8.GetBytes("VMDPI=240\r\nVMResWidth=1280\r\nVMResHeight=720\r\nVSyncEnabled=1\r\n");
        await File.WriteAllBytesAsync(configPath, bytes);
        await File.WriteAllBytesAsync(outside, "VMDPI=999\r\n"u8.ToArray());
        var before = Convert.ToHexString(SHA256.HashData(bytes));
        var written = File.GetLastWriteTimeUtc(configPath);
        var filesBefore = Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length;

        try
        {
            var reader = new WindowsGameLoopConfigReader();
            var userFile = Path.Combine(root, "user-not-created.ini");
            var discovery = new GameLoopConfigDiscovery(new FixtureReader(reader, userFile));
            var result = await discovery.DiscoverAsync([root]);

            Assert.Equal(before, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(configPath))));
            Assert.Equal(written, File.GetLastWriteTimeUtc(configPath));
            Assert.Equal(filesBefore, Directory.GetFiles(root, "*", SearchOption.AllDirectories).Length);
            Assert.True(result.Succeeded);
            Assert.NotNull(result.Value);
            Assert.Single(result.Value.Installs);
            Assert.Equal("240", result.Value.Installs[0].Settings.Dpi);
            Assert.Equal("1280×720", result.Value.Installs[0].Settings.Resolution);
            Assert.Equal("On", result.Value.Installs[0].Settings.VSync);
            Assert.Null(result.Value.Installs[0].Settings.Renderer);
            Assert.Contains(result.Value.Installs[0].Files, file =>
                file.Path.EndsWith(Path.Combine("ui", "config.ini"), StringComparison.OrdinalIgnoreCase)
                && file.Presence == ConfigPresence.Present
                && file.SizeBytes == bytes.Length);
            Assert.Contains(result.Value.Installs[0].Files, file =>
                file.Kind == ConfigFileKind.KeyMap && file.Presence == ConfigPresence.NotFound);
            Assert.DoesNotContain(result.Value.Installs[0].Files, file =>
                file.Path.Equals(outside, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            File.Delete(outside);
        }
    }

    [Fact]
    public async Task Empty_install_list_does_not_touch_the_reader()
    {
        var reader = new ScriptedReader { ThrowOnUse = true };
        var result = await new GameLoopConfigDiscovery(reader).DiscoverAsync([]);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Value!.Installs);
        Assert.Contains("not searched", result.Value.Notice, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, reader.Probes);
    }

    [Fact]
    public async Task Oversized_invalid_and_linked_files_are_not_parsed()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "glopt-cfg-" + Guid.NewGuid().ToString("N")));
        var configPath = Path.Combine(root, "ui", "config.ini");
        var jsonPath = Path.Combine(root, "user.json");
        var reader = new ScriptedReader
        {
            UserFiles = { jsonPath },
            Files =
            {
                [configPath] = new ConfigProbe { Exists = true, TooLarge = true, SizeBytes = 80_000 },
                [jsonPath] = new ConfigProbe { Exists = true, Text = "{" },
                [Path.Combine(root, "app.ini")] = new ConfigProbe { Exists = true, ReparsePoint = true }
            }
        };

        var result = await new GameLoopConfigDiscovery(reader).DiscoverAsync([root]);
        var files = result.Value!.Installs[0].Files;

        Assert.Contains(files, file => file.Path == configPath && file.Presence == ConfigPresence.Unreadable && file.Detail!.Contains("64 KB", StringComparison.Ordinal));
        Assert.Contains(files, file => file.Path == jsonPath && file.Presence == ConfigPresence.Unreadable);
        Assert.Contains(files, file => file.Path == Path.Combine(root, "app.ini") && file.Detail!.Contains("link", StringComparison.OrdinalIgnoreCase));
        Assert.Null(result.Value.Installs[0].Settings.Dpi);
    }

    [Fact]
    public async Task User_registry_applies_only_when_one_install_exists()
    {
        var first = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "glopt-a-" + Guid.NewGuid().ToString("N")));
        var second = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "glopt-b-" + Guid.NewGuid().ToString("N")));
        var registry = new RegistryProbe
        {
            Found = true,
            Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["VMDPI"] = "160",
                ["VMCpuCount"] = "2",
                ["VMMemorySizeInMB"] = "2048",
                ["FxaaQuality"] = "2"
            },
            UnmappedRendererKeys = ["ForceDirectX"]
        };
        var reader = new ScriptedReader { Registry = registry };

        var one = await new GameLoopConfigDiscovery(reader).DiscoverAsync([first]);
        Assert.Equal("160", one.Value!.Installs[0].Settings.Dpi);
        Assert.Equal("2", one.Value.Installs[0].Settings.CpuAllocation);
        Assert.Equal("2048 MB", one.Value.Installs[0].Settings.MemoryAllocation);
        Assert.Equal("2", one.Value.Installs[0].Settings.AntiAliasing);
        Assert.Null(one.Value.Installs[0].Settings.Renderer);
        Assert.Contains(one.Value.Installs[0].Files, file =>
            file.Kind == ConfigFileKind.Registry
            && file.Presence == ConfigPresence.Present
            && file.Detail!.Contains("ForceDirectX", StringComparison.Ordinal));

        var many = await new GameLoopConfigDiscovery(reader).DiscoverAsync([first, second]);
        Assert.Null(many.Value!.Installs[0].Settings.Dpi);
        Assert.Null(many.Value.Installs[1].Settings.Dpi);
        Assert.Equal("160", many.Value.SharedSettings!.Dpi);
        Assert.Contains("more than one", many.Value.Notice, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cancelled_discovery_returns_without_a_report()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await new GameLoopConfigDiscovery(new ScriptedReader()).DiscoverAsync(
            [Path.GetFullPath(Path.Combine(Path.GetTempPath(), "glopt-cancel"))],
            cts.Token);

        Assert.False(result.Succeeded);
        Assert.Null(result.Value);
        Assert.Contains("cancelled", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FixtureReader : IGameLoopConfigReader
    {
        private readonly WindowsGameLoopConfigReader _files = new();
        private readonly string _userFile;

        public FixtureReader(WindowsGameLoopConfigReader files, string userFile)
        {
            _files = files;
            _userFile = userFile;
        }

        public ConfigProbe ProbeFile(string path, bool readText) => _files.ProbeFile(path, readText);

        public RegistryProbe ReadMobileGamePc() => new();

        public IReadOnlyList<string> KnownUserFiles() => [_userFile];
    }

    private sealed class ScriptedReader : IGameLoopConfigReader
    {
        public Dictionary<string, ConfigProbe> Files { get; } = new(StringComparer.OrdinalIgnoreCase);

        public RegistryProbe Registry { get; set; } = new();

        public List<string> UserFiles { get; } = [];

        public List<SupplementalRegistry> Supplemental { get; } = [];

        public IReadOnlyList<SupplementalRegistry> ReadSupplementalRegistries() => Supplemental;

        public bool ThrowOnUse { get; init; }

        public int Probes { get; private set; }

        public ConfigProbe ProbeFile(string path, bool readText)
        {
            Probes++;
            if (ThrowOnUse)
            {
                throw new InvalidOperationException("reader");
            }

            return Files.TryGetValue(path, out var probe) ? probe : ConfigProbe.Missing();
        }

        public RegistryProbe ReadMobileGamePc()
        {
            Probes++;
            if (ThrowOnUse)
            {
                throw new InvalidOperationException("reader");
            }

            return Registry;
        }

        public IReadOnlyList<string> KnownUserFiles() => UserFiles;
    }
}

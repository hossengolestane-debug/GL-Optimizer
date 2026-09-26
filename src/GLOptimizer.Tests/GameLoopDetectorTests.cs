using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;
using GLOptimizer.GameLoop;

namespace GLOptimizer.Tests;

public class GameLoopDetectorTests
{
    [Fact]
    public async Task Verified_tree_reports_install_engine_processes_and_games()
    {
        using var tree = new TempInstall();
        var outside = Path.Combine(tree.Parent, "other", "unrelated.bin");
        Directory.CreateDirectory(Path.GetDirectoryName(outside)!);
        File.WriteAllText(outside, "outside");
        var before = Snapshot(tree.Parent);

        var environment = new FixtureEnvironment
        {
            Hints =
            {
                new UninstallHint
                {
                    DisplayName = "GameLoop",
                    InstallLocation = tree.Root,
                    DisplayVersion = "1.2.3"
                }
            },
            Processes = new ProcessQueryResult
            {
                Available = true,
                Processes =
                [
                    new ProcessObservation { ProcessId = 42, ProcessName = "AppMarket", ExecutablePath = Path.Combine(tree.Root, "AppMarket.exe") },
                    new ProcessObservation { ProcessId = 7, ProcessName = "AppMarket", ExecutablePath = outside }
                ]
            }
        };

        var result = await new GameLoopDetector(environment).DetectAsync();

        Assert.Equal(OperationStatus.Success, result.Status);
        var scan = result.Value!;
        var install = Assert.Single(scan.Installations);
        Assert.Equal(Path.GetFullPath(tree.Root), install.InstallPath);
        Assert.Equal("1.2.3", install.Version);
        Assert.Equal("AOW", install.Engine);
        Assert.Equal(GameRunStatus.Running, install.RunStatus);
        Assert.Equal(Path.GetFullPath(Path.Combine(tree.Root, "GameLoop.exe")), install.LauncherPath);
        var process = Assert.Single(install.Processes);
        Assert.Equal(42, process.ProcessId);
        Assert.Equal(GamePresenceStatus.Installed, scan.PubgMobile.Status);
        Assert.Equal("3.2.1", scan.PubgMobile.Version);
        Assert.Equal("com.tencent.ig", scan.PubgMobile.PackageId);
        Assert.Equal(GamePresenceStatus.Installed, scan.CodMobile.Status);
        Assert.Equal("1.0.4", scan.CodMobile.Version);
        Assert.False(scan.BrokenRegistration);
        Assert.Equal(before, Snapshot(tree.Parent));
    }

    [Fact]
    public async Task Directory_without_a_launcher_is_not_an_install()
    {
        var root = Path.Combine(Path.GetTempPath(), "glopt-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var environment = new FixtureEnvironment { Candidates = { root } };
            var result = await new GameLoopDetector(environment).DetectAsync();

            Assert.Empty(result.Value!.Installations);
            Assert.Equal(GamePresenceStatus.Unknown, result.Value.PubgMobile.Status);
            Assert.Equal(GamePresenceStatus.Unknown, result.Value.CodMobile.Status);
            Assert.False(result.Value.BrokenRegistration);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Conflicting_versions_stay_unknown()
    {
        using var tree = new TempInstall();
        var environment = new FixtureEnvironment
        {
            Hints =
            {
                new UninstallHint { DisplayName = "GameLoop", InstallLocation = tree.Root, DisplayVersion = "1.0.0" },
                new UninstallHint { DisplayName = "TxGameAssistant", DisplayIcon = Path.Combine(tree.Root, "GameLoop.exe") + ",0", DisplayVersion = "2.0.0" }
            }
        };

        var result = await new GameLoopDetector(environment).DetectAsync();

        Assert.Null(Assert.Single(result.Value!.Installations).Version);
        Assert.Contains(result.Value.Warnings, warning => warning.Contains("Conflicting", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Conflicting_package_versions_do_not_invent_one()
    {
        using var tree = new TempInstall();
        File.WriteAllText(Path.Combine(tree.Root, "ui", "Android", "data", "com.tencent.ig", "info.json"), "\"Version\": \"9.9.9\"");
        var environment = new FixtureEnvironment { Candidates = { tree.Root } };
        var result = await new GameLoopDetector(environment).DetectAsync();

        Assert.Equal(GamePresenceStatus.Installed, result.Value!.PubgMobile.Status);
        Assert.Null(result.Value.PubgMobile.Version);
    }

    [Fact]
    public async Task Incomplete_package_search_is_unknown_not_missing()
    {
        using var tree = new TempInstall();
        var environment = new FixtureEnvironment
        {
            Candidates = { tree.Root },
            ForcedSearch = new DirectorySearchResult { Completed = false }
        };
        RemovePackages(tree.Root);

        var result = await new GameLoopDetector(environment).DetectAsync();

        Assert.Equal(GamePresenceStatus.Unknown, result.Value!.PubgMobile.Status);
        Assert.Equal(GamePresenceStatus.Unknown, result.Value.CodMobile.Status);
    }

    [Fact]
    public async Task Completed_search_with_no_package_is_not_found()
    {
        using var tree = new TempInstall();
        RemovePackages(tree.Root);
        var environment = new FixtureEnvironment { Candidates = { tree.Root } };

        var result = await new GameLoopDetector(environment).DetectAsync();

        Assert.Equal(GamePresenceStatus.NotFound, result.Value!.PubgMobile.Status);
        Assert.Equal(GamePresenceStatus.NotFound, result.Value.CodMobile.Status);
        Assert.Null(result.Value.PubgMobile.Version);
    }

    [Fact]
    public async Task Missing_uninstall_path_without_an_install_is_broken()
    {
        var environment = new FixtureEnvironment
        {
            Hints = { new UninstallHint { DisplayName = "GameLoop", InstallLocation = Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid().ToString("N")), DisplayVersion = "9.9.9" } }
        };

        var result = await new GameLoopDetector(environment).DetectAsync();

        Assert.Empty(result.Value!.Installations);
        Assert.True(result.Value.BrokenRegistration);
        Assert.Equal(GamePresenceStatus.Unknown, result.Value.PubgMobile.Status);
    }

    [Fact]
    public async Task Unreadable_process_does_not_become_stopped()
    {
        using var tree = new TempInstall();
        var environment = new FixtureEnvironment
        {
            Candidates = { tree.Root },
            Processes = new ProcessQueryResult { Available = true, HadUnreadableMatch = true }
        };

        var result = await new GameLoopDetector(environment).DetectAsync();

        Assert.Equal(GameRunStatus.Unknown, Assert.Single(result.Value!.Installations).RunStatus);
    }

    [Fact]
    public async Task Cancelled_scan_fails_without_a_value()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await new GameLoopDetector(new FixtureEnvironment()).DetectAsync(cts.Token);

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Null(result.Value);
        Assert.Contains("cancelled", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Shortcut_under_the_install_resolves_the_parent()
    {
        using var tree = new TempInstall();
        var emulator = Path.Combine(tree.Root, "ui", "AndroidEmulator.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(emulator)!);
        File.WriteAllText(emulator, "emulator");
        File.Delete(Path.Combine(tree.Root, "GameLoop.exe"));
        File.Delete(Path.Combine(tree.Root, "AppMarket.exe"));
        var environment = new FixtureEnvironment { Shortcuts = { emulator } };

        var result = await new GameLoopDetector(environment).DetectAsync();

        var install = Assert.Single(result.Value!.Installations);
        Assert.Equal(Path.GetFullPath(tree.Root), install.InstallPath);
        Assert.Equal(Path.GetFullPath(emulator), install.LauncherPath);
        Assert.Equal("AOW", install.Engine);
    }

    private static void RemovePackages(string root)
    {
        var data = Path.Combine(root, "ui", "Android", "data");
        if (Directory.Exists(data))
        {
            Directory.Delete(data, true);
        }
    }

    private static HashSet<string> Snapshot(string root)
    {
        return Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => path.Replace('\\', '/'))
            .ToHashSet(StringComparer.Ordinal);
    }

    private sealed class TempInstall : IDisposable
    {
        public TempInstall()
        {
            Parent = Path.Combine(Path.GetTempPath(), "glopt-scan-" + Guid.NewGuid().ToString("N"));
            Root = Path.Combine(Parent, "GameLoop");
            Directory.CreateDirectory(Path.Combine(Root, "Engine"));
            var pubg = Path.Combine(Root, "ui", "Android", "data", "com.tencent.ig");
            var cod = Path.Combine(Root, "ui", "Android", "data", "com.activision.callofduty.shooter");
            Directory.CreateDirectory(pubg);
            Directory.CreateDirectory(cod);
            File.WriteAllText(Path.Combine(Root, "GameLoop.exe"), "launcher");
            File.WriteAllText(Path.Combine(Root, "AppMarket.exe"), "market");
            File.WriteAllText(Path.Combine(Root, "Engine", "aow_exe.exe"), "engine");
            File.WriteAllText(Path.Combine(pubg, "version.txt"), "versionName=3.2.1\n");
            File.WriteAllText(Path.Combine(cod, "version.txt"), "1.0.4\n");
        }

        public string Parent { get; }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Parent))
            {
                Directory.Delete(Parent, true);
            }
        }
    }

    private sealed class FixtureEnvironment : IGameLoopEnvironment
    {
        public List<UninstallHint> Hints { get; } = [];

        public List<string> Candidates { get; } = [];

        public List<string> Shortcuts { get; } = [];

        public List<string> DataRoots { get; } = [];

        public ProcessQueryResult Processes { get; set; } = new() { Available = true };

        public DirectorySearchResult? ForcedSearch { get; set; }

        public IReadOnlyList<UninstallHint> ReadUninstallHints() => Hints;

        public IReadOnlyList<string> CandidateDirectories() => Candidates;

        public IReadOnlyList<string> StartMenuTargets() => Shortcuts;

        public IReadOnlyList<string> MobileDataRoots() => DataRoots;

        public ProcessQueryResult QueryProcesses() => Processes;

        public bool FileExists(string path)
        {
            try
            {
                return File.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool DirectoryExists(string path)
        {
            try
            {
                return Directory.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public DirectorySearchResult FindDirectoriesNamed(string root, IReadOnlyCollection<string> names, int maxDepth, int maxNodes, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ForcedSearch ?? DirectoryWalker.FindNamed(root, names, maxDepth, maxNodes, cancellationToken);
        }

        public string? ReadSmallText(string path, int maxBytes)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                var info = new FileInfo(path);
                if (info.Length <= 0 || info.Length > maxBytes)
                {
                    return null;
                }

                return File.ReadAllText(path);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public string? TryReadFileVersion(string path) => null;
    }
}

using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;
using GLOptimizer.Core.Settings;
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
    public void Uninstall_string_keeps_the_quoted_path_and_drops_arguments()
    {
        var file = Path.Combine(Path.GetTempPath(), "Uninstall.exe");
        var command = "\"" + file + "\" --oem-uninstall=0 --uninstall-entry=2";

        Assert.Equal(Path.GetFullPath(file), InstallPathRules.TryNormalizeCommand(command));
    }

    [Fact]
    public async Task Real_7x_layout_is_found_without_a_process_path()
    {
        var parent = Path.Combine(Path.GetTempPath(), "glopt-7x-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parent, "GameLoop");
        var application = Path.Combine(root, "Application");
        var data = Path.Combine(parent, "GameLoopData");
        var tencent = Path.Combine(parent, "Tencent");
        var market = Path.Combine(parent, "MobileGamePC");
        var pubg = Path.Combine(tencent, "GameLoop", "apps", "com.tencent.ig");
        Directory.CreateDirectory(application);
        Directory.CreateDirectory(Path.Combine(data, "Component", "GameLoop"));
        Directory.CreateDirectory(pubg);
        Directory.CreateDirectory(Path.Combine(market, "AppMarket3"));
        var launcher = Path.Combine(application, "GameLoopLauncher.exe");
        var uninstall = Path.Combine(application, "Uninstall.exe");
        File.WriteAllText(launcher, "launcher");
        File.WriteAllText(uninstall, "uninstall");
        File.WriteAllText(Path.Combine(pubg, "version.txt"), "versionName=3.4.0\n");
        File.WriteAllText(Path.Combine(market, "AppMarket3", "apklocalpkgs.json"), "{}");
        try
        {
            var environment = new FixtureEnvironment
            {
                Hints =
                {
                    new UninstallHint
                    {
                        DisplayName = "GameLoop",
                        InstallLocation = string.Empty,
                        DisplayIcon = uninstall,
                        UninstallString = "\"" + uninstall + "\" --oem-uninstall=0 --uninstall-entry=2",
                        DisplayVersion = "7.0.19.05",
                        Source = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\GameLoop (64-bit)"
                    }
                },
                Processes = new ProcessQueryResult
                {
                    Available = true,
                    HadUnreadableMatch = true,
                    Processes =
                    [
                        new ProcessObservation { ProcessId = 11, ProcessName = "GameLoop" },
                        new ProcessObservation { ProcessId = 12, ProcessName = "GameLoopAssistant" },
                        new ProcessObservation { ProcessId = 13, ProcessName = "GameLoopDldSvr" },
                        new ProcessObservation { ProcessId = 14, ProcessName = "GameLoopEmulator" },
                        new ProcessObservation { ProcessId = 15, ProcessName = "GameLoopService" },
                        new ProcessObservation { ProcessId = 16, ProcessName = "GameLoopVm" }
                    ]
                }
            };
            environment.Registrations.Add(new ProductRegistration
            {
                Location = @"HKLM\SOFTWARE\Tencent\GameLoop (64-bit)",
                Found = true,
                InstallPath = root,
                DataPath = data,
                Version = "7.0.19.05"
            });
            environment.Registrations.Add(new ProductRegistration
            {
                Location = @"HKLM\SOFTWARE\WOW6432Node\Tencent\GameLoop (64-bit)",
                Found = false
            });
            environment.DataRoots.Add(tencent);
            environment.Markets.Add(market);
            environment.FileVersions[Path.GetFullPath(launcher)] = "7.0.167.0";
            var log = new ListLog();

            var result = await new GameLoopDetector(environment).DetectAsync();
            ScanCheckLog.Write(log, result.Value);

            Assert.Equal(OperationStatus.Success, result.Status);
            var scan = result.Value!;
            var install = Assert.Single(scan.Installations);
            Assert.Equal(Path.GetFullPath(root), install.InstallPath);
            Assert.Equal("7.0.19.05", install.Version);
            Assert.Equal(GameRunStatus.Running, install.RunStatus);
            Assert.Equal(Path.GetFullPath(launcher), install.LauncherPath);
            Assert.Equal(Path.GetFullPath(data), install.DataPath);
            Assert.Equal(6, install.Processes.Count);
            Assert.All(install.Processes, process => Assert.Equal(string.Empty, process.ExecutablePath));
            Assert.Equal(GamePresenceStatus.Installed, scan.PubgMobile.Status);
            Assert.Equal("3.4.0", scan.PubgMobile.Version);
            Assert.Equal("com.tencent.ig", scan.PubgMobile.PackageId);
            Assert.Equal(GamePresenceStatus.NotFound, scan.CodMobile.Status);
            Assert.Contains(Path.GetFullPath(market), scan.MarketRoots);
            Assert.Contains(scan.Checks, check => check.Kind == "Registry" && check.Found && check.Target.Contains(@"SOFTWARE\Tencent\GameLoop", StringComparison.Ordinal));
            Assert.Contains(scan.Checks, check => check.Kind == "Registry" && !check.Found && check.Target.Contains("WOW6432Node", StringComparison.Ordinal));
            Assert.Contains(scan.Checks, check => check.Kind == "Registry" && check.Found && check.Target.Contains("Uninstall\\GameLoop", StringComparison.Ordinal));
            Assert.Contains(scan.Checks, check => check.Kind == "Process" && check.Target == "GameLoopAssistant" && check.Found && check.Detail == "path unavailable");
            Assert.Contains(scan.Checks, check => check.Kind == "Process" && check.Target == "aow_exe" && !check.Found);
            Assert.Contains(scan.Checks, check => check.Kind == "Path" && check.Found && check.Detail == "GameLoopData");
            Assert.Contains(log.Messages, message => message.Contains(@"SOFTWARE\Tencent\GameLoop", StringComparison.Ordinal) && message.Contains("found", StringComparison.Ordinal));
            Assert.Contains(log.Messages, message => message.Contains("aow_exe", StringComparison.Ordinal) && message.Contains("not found", StringComparison.Ordinal));
            Assert.False(scan.BrokenRegistration);
        }
        finally
        {
            if (Directory.Exists(parent))
            {
                Directory.Delete(parent, true);
            }
        }
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

        public List<ProductRegistration> Registrations { get; } = [];

        public List<string> Markets { get; } = [];

        public Dictionary<string, string> FileVersions { get; } = new(StringComparer.OrdinalIgnoreCase);

        public ProcessQueryResult Processes { get; set; } = new() { Available = true };

        public DirectorySearchResult? ForcedSearch { get; set; }

        public IReadOnlyList<UninstallHint> ReadUninstallHints() => Hints;

        public IReadOnlyList<ProductRegistration> ReadProductRegistrations() => Registrations;

        public IReadOnlyList<string> MarketDirectories() => Markets;

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

        public string? TryReadFileVersion(string path)
        {
            try
            {
                var full = Path.GetFullPath(path);
                return FileVersions.TryGetValue(full, out var version) ? version : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    private sealed class ListLog : ILogStore
    {
        public List<string> Messages { get; } = [];

        public string LogDirectory => string.Empty;

        public string ActiveLogFilePath => string.Empty;

        public LogSeverity MinimumLevel => LogSeverity.Trace;

        public string? LastError => null;

        public void ApplyPolicy(AppSettings settings)
        {
        }

        public void Write(LogSeverity severity, string category, string message, Exception? exception = null)
        {
            Messages.Add(category + " " + message);
        }

        public IReadOnlyList<LogEntry> GetRecent(int count = 200) => [];

        public IReadOnlyList<LogEntry> ReadActiveLog(int maxLines = 500) => [];

        public void Flush()
        {
        }
    }
}

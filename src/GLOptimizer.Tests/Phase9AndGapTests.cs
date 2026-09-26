using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Backup;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Elevation;
using GLOptimizer.Core.Launch;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Monitoring;
using GLOptimizer.Core.Network;
using GLOptimizer.Core.Notifications;
using GLOptimizer.Core.Optimization;
using GLOptimizer.Core.Repair;
using GLOptimizer.Core.Results;
using GLOptimizer.Core.Settings;
using GLOptimizer.Core.Updates;
using GLOptimizer.GameLoop;
using GLOptimizer.Infrastructure.Network;
using GLOptimizer.Infrastructure.Updates;

namespace GLOptimizer.Tests;

public class Phase9AndGapTests
{
    [Fact]
    public async Task Pubg_compares_local_and_market_versions_without_an_official_host()
    {
        var root = NewTemp();
        try
        {
            var package = Path.Combine(root, "ui", "Android", "data", "com.tencent.ig");
            Directory.CreateDirectory(package);
            File.WriteAllText(Path.Combine(package, "version.txt"), "2.0.0\n");
            var market = Path.Combine(root, "AppMarket", "com.tencent.ig.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(market)!);
            File.WriteAllText(market, "1.0.0\n");
            Directory.CreateDirectory(Path.Combine(root, "ui"));
            File.WriteAllText(Path.Combine(root, "ui", "log.txt"), "engine error: surface\n");

            var harness = Harness(root, package, market);
            harness.Processes.Items.Add(new ControlledProcess
            {
                ProcessId = 4,
                ProcessName = "aow_exe",
                ExecutablePath = Path.Combine(root, "aow_exe.exe")
            });
            harness.Windows.Result = OperationResult<IReadOnlyList<WindowTitle>>.Success(
            [
                new WindowTitle { ProcessId = 99, Title = "PUBG MOBILE" }
            ]);
            harness.Monitor.First = 3;
            harness.Monitor.Second = 9;

            var report = await harness.Diagnostics.RunAsync();
            Assert.True(report.Succeeded);
            Assert.NotNull(report.Value);
            Assert.Equal("2.0.0", report.Value.InstalledVersion);
            Assert.Equal("1.0.0", report.Value.MarketVersion);
            Assert.Null(report.Value.OfficialVersion);
            Assert.False(report.Value.OfficialRequested);
            Assert.Equal(CatalogComparison.VersionMismatch, report.Value.Comparison);
            Assert.Contains("PUBG Mobile version mismatch", report.Value.Issue, StringComparison.Ordinal);
            Assert.Equal("Not running", report.Value.LaunchStatus);
            Assert.Equal("9%", report.Value.CpuDetail);
            Assert.Contains("engine error: surface", report.Value.LogDetail, StringComparison.Ordinal);
            Assert.DoesNotContain("http", report.Value.OfficialDetail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Pubg_match_and_ambiguous_market_stay_honest()
    {
        var root = NewTemp();
        try
        {
            var package = Path.Combine(root, "ui", "Android", "data", "com.tencent.ig");
            Directory.CreateDirectory(package);
            File.WriteAllText(Path.Combine(package, "version.txt"), "1.0.0\n");
            var market = Path.Combine(root, "AppMarket", "com.tencent.ig.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(market)!);
            File.WriteAllText(market, "1.0.0\n");
            var matched = await Harness(root, package, market).Diagnostics.RunAsync();
            Assert.Equal(CatalogComparison.Match, matched.Value?.Comparison);

            var other = Path.Combine(root, "AppMarket", "other.txt");
            File.WriteAllText(other, "com.tencent.ig\nversionName=2.0.0\n");
            var inventory = new[]
            {
                Metadata(market, "AppMarket/com.tencent.ig.txt"),
                Metadata(other, "AppMarket/other.txt")
            };
            var ambiguous = await Harness(root, package, market, inventory).Diagnostics.RunAsync();
            Assert.Null(ambiguous.Value?.MarketVersion);
            Assert.Equal(CatalogComparison.Unknown, ambiguous.Value?.Comparison);
            Assert.False(ambiguous.Value?.OfficialRequested);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Pubg_process_inside_the_install_is_running()
    {
        var root = NewTemp();
        try
        {
            var package = Path.Combine(root, "ui", "Android", "data", "com.tencent.ig");
            Directory.CreateDirectory(package);
            var harness = Harness(root, package, null, null);
            harness.Processes.Items.Add(new ControlledProcess
            {
                ProcessId = 8,
                ProcessName = "pubg",
                ExecutablePath = Path.Combine(package, "pubg.exe")
            });
            var report = await harness.Diagnostics.RunAsync();
            Assert.Equal("Running", report.Value?.LaunchStatus);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Report_replaces_the_user_profile_and_keeps_no_token()
    {
        var profile = Path.Combine("C:", "Users", "someone");
        var lines = new[]
        {
            new DiagnosticReportLine
            {
                Title = "App data",
                Detail = Path.Combine(profile, "GLOptimizer", "settings.json"),
                Badge = "Ready"
            }
        };
        var text = DiagnosticReportBuilder.ToText(lines, profile);
        var json = DiagnosticReportBuilder.ToJson(lines, profile);
        Assert.Contains("%USERPROFILE%", text, StringComparison.Ordinal);
        Assert.Contains("%USERPROFILE%", json, StringComparison.Ordinal);
        Assert.DoesNotContain("someone", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("someone", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Report_redacts_both_slash_styles()
    {
        var profile = @"C:\Users\someone";
        var text = ReportRedaction.Redact(@"C:/Users/someone/GLOptimizer and C:\Users\Someone\Logs", profile);
        Assert.Equal(@"%USERPROFILE%/GLOptimizer and %USERPROFILE%\Logs", text);
    }

    [Fact]
    public async Task Launch_optimized_journal_survives_until_recovery()
    {
        var root = NewTemp();
        try
        {
            var game = Path.Combine(root, "GameLoop.exe");
            var priority = new MemoryPriority();
            priority.Running.Add(10);
            priority.Values[10] = "Normal";
            var journal = new MemoryJournal();
            var service = new LaunchOptimizedService(
                new FixedDetector(root),
                new FakeProcesses
                {
                    Items =
                    {
                        new ControlledProcess { ProcessId = 10, ProcessName = "GameLoop", ExecutablePath = game },
                        new ControlledProcess { ProcessId = 11, ProcessName = "notepad", ExecutablePath = Path.Combine(root, "notepad.exe") },
                        new ControlledProcess { ProcessId = 12, ProcessName = "GameLoop", ExecutablePath = Path.Combine(Path.GetTempPath(), "elsewhere", "GameLoop.exe") }
                    }
                },
                priority,
                journal,
                new FixedClock());

            var realtime = LaunchOptimizedRules.Plan(
                [new ControlledProcess { ProcessId = 10, ProcessName = "GameLoop", ExecutablePath = game }],
                [root],
                PriorityNames.Realtime);
            Assert.False(realtime.CanApply);

            var applied = await service.ApplyAsync(confirmed: true);
            Assert.True(applied.Succeeded);
            Assert.Equal("AboveNormal", priority.Values[10]);
            Assert.DoesNotContain(priority.Sets, set => set.Id != 10);
            Assert.NotNull(journal.Current);
            Assert.Equal("Normal", journal.Current!.Changes[0].PreviousPriority);

            var again = new LaunchOptimizedService(new FixedDetector(root), new FakeProcesses(), priority, journal, new FixedClock());
            var waiting = again.Inspect();
            Assert.True(waiting.JournalPresent);

            priority.Running.Add(10);
            var restored = await again.RecoverAsync(restore: true);
            Assert.True(restored.Succeeded);
            Assert.Equal("Normal", priority.Values[10]);
            Assert.Null(journal.Current);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Launch_optimized_dismiss_clears_the_journal_without_restoring()
    {
        var journal = new MemoryJournal();
        journal.Save(new LaunchJournal
        {
            Changes = [new PriorityChange { ProcessId = 3, PreviousPriority = "Normal", ExecutablePath = "GameLoop.exe" }]
        });
        var priority = new MemoryPriority();
        priority.Running.Add(3);
        priority.Values[3] = "AboveNormal";
        var service = new LaunchOptimizedService(new FixedDetector(Path.Combine(Path.GetTempPath(), "glopt-unused")), new FakeProcesses(), priority, journal, new FixedClock());
        var result = await service.RecoverAsync(restore: false);
        Assert.True(result.Succeeded);
        Assert.Equal("AboveNormal", priority.Values[3]);
        Assert.Null(journal.Current);
        Assert.Equal(OperationStatus.NotImplemented, service.PowerPlan().Status);
        Assert.Equal(OperationStatus.NotImplemented, service.GraphicsPreference().Status);
    }

    [Fact]
    public void Launch_session_clears_the_journal_after_the_process_exits()
    {
        var journal = new MemoryJournal();
        journal.Save(new LaunchJournal
        {
            Changes = [new PriorityChange { ProcessId = 5, PreviousPriority = "Normal" }]
        });
        var priority = new MemoryPriority();
        var log = new MemoryLog();
        var watcher = new LaunchSessionWatcher(journal, priority, log);
        priority.Running.Add(5);
        watcher.CheckOnce();
        Assert.NotNull(journal.Current);
        priority.Running.Remove(5);
        watcher.CheckOnce();
        Assert.Null(journal.Current);
        Assert.Contains(log.Messages, message => message.Contains("exited", StringComparison.Ordinal));
    }

    [Fact]
    public void Start_with_windows_writes_and_removes_only_the_named_value()
    {
        var startup = new MemoryStartup();
        var enabled = StartupRegistrationRules.Apply(startup, true, @"C:\GLOptimizer\GLOptimizer.exe");
        Assert.True(enabled.Succeeded);
        Assert.Equal([StartupRegistrationRules.ValueName], startup.Values.Keys);
        Assert.Equal(@"C:\GLOptimizer\GLOptimizer.exe", startup.Values[StartupRegistrationRules.ValueName]);

        var refused = startup.SetCommand("Other", "nope");
        Assert.False(refused.Succeeded);
        Assert.Single(startup.Values);

        var removed = StartupRegistrationRules.Apply(startup, false, string.Empty);
        Assert.True(removed.Succeeded);
        Assert.Empty(startup.Values);
        Assert.True(StartupRegistrationRules.Apply(startup, false, string.Empty).Succeeded);

        startup.IsSupported = false;
        var blocked = StartupRegistrationRules.Apply(startup, true, "command");
        Assert.False(blocked.Succeeded);
        Assert.Empty(startup.Values);
    }

    [Fact]
    public void Settings_reject_an_unverified_override_and_keep_dark_theme()
    {
        var parent = NewTemp();
        var root = Path.Combine(parent, "install");
        Directory.CreateDirectory(root);
        try
        {
            Assert.NotNull(InstallOverrideRules.Validate(root));
            File.WriteAllText(Path.Combine(root, "GameLoop.exe"), "launcher");
            Assert.Null(InstallOverrideRules.Validate(root));
            Assert.Null(InstallOverrideRules.Validate("  "));

            var link = Path.Combine(parent, "linked-install");
            Directory.CreateSymbolicLink(link, root);
            Assert.NotNull(InstallOverrideRules.Validate(link));

            var settings = new AppSettings
            {
                Theme = (AppTheme)99,
                GameLoopPathOverride = "   ",
                DeveloperSimulationEnabled = true
            };
            AppSettingsRules.Normalize(settings);
            Assert.Equal(AppTheme.Dark, settings.Theme);
            Assert.Null(settings.GameLoopPathOverride);
            Assert.Equal(AppTheme.Dark, ThemePolicy.Applied(AppTheme.Light));
            Assert.False(ThemePolicy.IsImplemented(AppTheme.Light));
#if DEBUG
            Assert.True(DeveloperSimulation.IsAvailable);
#else
            Assert.False(DeveloperSimulation.IsAvailable);
            Assert.Null(DeveloperSimulation.Scan());
            Assert.False(settings.DeveloperSimulationEnabled);
#endif
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public async Task Update_metadata_is_validated_and_the_stub_makes_no_request()
    {
        Assert.Null(UpdateMetadataRules.Validate("1.2.3", new string('a', 64), "https://example.com/GLOptimizer.zip"));
        Assert.NotNull(UpdateMetadataRules.Validate("1.2.3", new string('a', 64), "http://example.com/GLOptimizer.zip"));
        Assert.NotNull(UpdateMetadataRules.Validate("1.2.3", new string('a', 64), "https://user:pass@example.com/GLOptimizer.zip"));
        Assert.NotNull(UpdateMetadataRules.Validate("1.2.3", "abc", "https://example.com/GLOptimizer.zip"));
        Assert.NotNull(UpdateMetadataRules.Validate("beta", new string('a', 64), "https://example.com/GLOptimizer.zip"));

        var updates = new NotImplementedUpdateService();
        var result = await updates.CheckAsync();
        Assert.Equal(OperationStatus.NotImplemented, result.Status);
        Assert.Equal(0, updates.NetworkRequests);
    }

    [Fact]
    public async Task Network_diagnostics_probe_only_the_allowlist()
    {
        var probe = new RecordingProbe();
        var service = new NetworkDiagnosticsService(new FixedNetwork(), probe);
        var report = await service.RunAsync();
        Assert.True(report.Succeeded);
        Assert.True(report.Value?.RequestedNetwork);
        Assert.Equal(NetworkAllowlist.Hosts, probe.Calls.Select(call => call.Host));
        Assert.All(probe.Calls, call => Assert.Equal(NetworkAllowlist.Port, call.Port));

        var refused = await new TcpConnectProbe().ProbeAsync("example.com", 443);
        Assert.False(refused.Succeeded);
        Assert.Empty(probe.Calls.Where(call => call.Host == "example.com"));
    }

    [Fact]
    public void Toasts_accept_only_the_catalog_and_skip_repeats()
    {
        var deduper = new ToastDeduper(TimeSpan.FromMinutes(2));
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        Assert.False(deduper.TryAccept("Hello", now));
        Assert.True(deduper.TryAccept(ToastCatalog.SettingsSaved, now));
        Assert.False(deduper.TryAccept(ToastCatalog.SettingsSaved, now.AddMinutes(1)));
        Assert.True(deduper.TryAccept(ToastCatalog.BackupCreated, now));
    }

    [Fact]
    public void Elevation_accepts_only_the_two_operations()
    {
        Assert.Equal("launch-optimized", ElevationPolicy.Read(["--operation", "launch-optimized"]));
        Assert.Equal("restore-priority", ElevationPolicy.Read(["GLOptimizer.exe", "--operation", "restore-priority"]));
        Assert.Null(ElevationPolicy.Read(["--operation", "repair"]));
        Assert.Null(ElevationPolicy.Argument("repair"));
        Assert.Contains("relaunch only this operation", ElevationPolicy.Explain("Launch Optimized"), StringComparison.OrdinalIgnoreCase);
    }

    private static PubgHarness Harness(
        string root,
        string package,
        string? marketVersionPath,
        IReadOnlyList<MarketInventoryItem>? inventory = null)
    {
        inventory ??= marketVersionPath is null
            ? []
            : [Metadata(marketVersionPath, "AppMarket/com.tencent.ig.txt")];
        return new PubgHarness(root, package, inventory);
    }

    private static MarketInventoryItem Metadata(string path, string relative) => new()
    {
        Path = path,
        RelativePath = relative,
        Kind = MarketItemKind.Metadata,
        Reason = "metadata"
    };

    private static string NewTemp()
    {
        var path = Path.Combine(Path.GetTempPath(), "glopt-phase9-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class PubgHarness
    {
        public PubgHarness(string root, string package, IReadOnlyList<MarketInventoryItem> inventory)
        {
            Processes = new FakeProcesses();
            Windows = new FakeWindows();
            Monitor = new ScriptedMonitor();
            var market = new FakeMarket
            {
                Report = new AppMarketReport
                {
                    InstallPaths = [root],
                    Inventory = inventory
                }
            };
            Diagnostics = new PubgMobileDiagnostics(
                new FixedDetector(root, package),
                market,
                new AppMarketVersionService(),
                new CodMobileVersionChecker(),
                Processes,
                Windows,
                Monitor,
                new ImmediateDelay(),
                new FixedClock(),
                new EmptyOptimization(),
                new OptimizationProfileSelection());
        }

        public FakeProcesses Processes { get; }

        public FakeWindows Windows { get; }

        public ScriptedMonitor Monitor { get; }

        public PubgMobileDiagnostics Diagnostics { get; }
    }

    private sealed class FixedDetector : IGameLoopDetector
    {
        private readonly string _root;
        private readonly string? _package;

        public FixedDetector(string root, string? package = null)
        {
            _root = root;
            _package = package;
        }

        public Task<OperationResult<GameLoopScan>> DetectAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult<GameLoopScan>.Success(new GameLoopScan
            {
                Installations = [new GameLoopInstallation { InstallPath = _root, LauncherPath = Path.Combine(_root, "GameLoop.exe") }],
                PubgMobile = new MobileGamePresence
                {
                    Status = _package is null ? GamePresenceStatus.NotFound : GamePresenceStatus.Installed,
                    Path = _package,
                    PackageId = "com.tencent.ig",
                    Detail = _package is null ? "Not found." : "Installed."
                }
            }));
    }

    private sealed class FakeMarket : IAppMarketDiagnostics
    {
        public AppMarketReport Report { get; set; } = new();

        public Task<OperationResult<AppMarketReport>> ScanAsync(bool checkOfficialVersion, CancellationToken cancellationToken = default)
        {
            Assert.False(checkOfficialVersion);
            return Task.FromResult(OperationResult<AppMarketReport>.Success(Report));
        }
    }

    private sealed class FakeProcesses : IProcessControl
    {
        public List<ControlledProcess> Items { get; } = [];

        public IReadOnlyList<ControlledProcess> List() => Items;

        public bool TryCloseMainWindow(int processId) => false;

        public bool TryTerminate(int processId) => false;
    }

    private sealed class FakeWindows : IWindowTitleSource
    {
        public OperationResult<IReadOnlyList<WindowTitle>> Result { get; set; } =
            OperationResult<IReadOnlyList<WindowTitle>>.Success([]);

        public OperationResult<IReadOnlyList<WindowTitle>> List() => Result;
    }

    private sealed class ScriptedMonitor : IGameLoopMonitor
    {
        public double? First { get; set; }

        public double? Second { get; set; }

        private int _reads;

        public Task WarmAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public GameLoopReading Read(DateTimeOffset now)
        {
            var cpu = _reads == 0 ? First : Second;
            _reads++;
            return new GameLoopReading { CpuPercent = cpu };
        }
    }

    private sealed class ImmediateDelay : ISampleDelay
    {
        public Task WaitAsync(TimeSpan interval, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
    }

    private sealed class EmptyOptimization : IOptimizationService
    {
        public Task<OperationResult<OptimizationAnalysis>> AnalyzeAsync(OptimizationProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult<OptimizationAnalysis>.Success(new OptimizationAnalysis
            {
                Recommendations = [new OptimizationRecommendation { Setting = "Renderer", Reason = "test", Status = RecommendationStatus.Applicable }]
            }));

        public Task<OperationResult<OptimizationPreview>> PreviewAsync(OptimizationProfile profile, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult<OptimizationPreview>.NotImplemented("Preview"));

        public Task<OperationResult<OptimizationReport>> ApplyAsync(OptimizationProfile profile, bool confirmed, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult<OptimizationReport>.NotImplemented("Apply"));

        public Task<OperationResult<RestoreReport>> UndoLastAsync(bool confirmed, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult<RestoreReport>.NotImplemented("Undo"));
    }

    private sealed class MemoryPriority : IProcessPriority
    {
        public Dictionary<int, string> Values { get; } = [];

        public HashSet<int> Running { get; } = [];

        public List<(int Id, string Priority)> Sets { get; } = [];

        public bool IsRunning(int processId) => Running.Contains(processId);

        public string? ReadPriority(int processId) => Values.TryGetValue(processId, out var value) ? value : null;

        public bool TrySet(int processId, string priorityName)
        {
            if (priorityName.Equals(PriorityNames.Realtime, StringComparison.OrdinalIgnoreCase) || !PriorityNames.CanRestore(priorityName) && !PriorityNames.CanApply(priorityName))
            {
                return false;
            }

            Sets.Add((processId, priorityName));
            Values[processId] = priorityName;
            return true;
        }
    }

    private sealed class MemoryJournal : ILaunchJournalStore
    {
        public LaunchJournal? Current { get; private set; }

        public LaunchJournal? Load() => Current;

        public void Save(LaunchJournal journal) => Current = journal;

        public void Clear() => Current = null;
    }

    private sealed class MemoryStartup : IStartupRegistration
    {
        public bool IsSupported { get; set; } = true;

        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

        public OperationResult<string?> ReadCommand(string valueName) =>
            OperationResult<string?>.Success(Values.TryGetValue(valueName, out var value) ? value : null);

        public OperationResult SetCommand(string valueName, string command)
        {
            if (!string.Equals(valueName, StartupRegistrationRules.ValueName, StringComparison.Ordinal))
            {
                return OperationResult.Failure("Only the GL Optimizer startup value can be written.");
            }

            Values[valueName] = command;
            return OperationResult.Success();
        }

        public OperationResult Remove(string valueName)
        {
            if (!string.Equals(valueName, StartupRegistrationRules.ValueName, StringComparison.Ordinal))
            {
                return OperationResult.Failure("Only the GL Optimizer startup value can be removed.");
            }

            Values.Remove(valueName);
            return OperationResult.Success();
        }
    }

    private sealed class MemoryLog : ILogStore
    {
        public List<string> Messages { get; } = [];

        public string LogDirectory => string.Empty;

        public string ActiveLogFilePath => string.Empty;

        public LogSeverity MinimumLevel => LogSeverity.Trace;

        public string? LastError => null;

        public void ApplyPolicy(AppSettings settings)
        {
        }

        public void Write(LogSeverity severity, string category, string message, Exception? exception = null) => Messages.Add(message);

        public IReadOnlyList<LogEntry> GetRecent(int count = 200) => [];

        public IReadOnlyList<LogEntry> ReadActiveLog(int maxLines = 500) => [];

        public void Flush()
        {
        }
    }

    private sealed class FixedNetwork : INetworkStatus
    {
        public bool IsAvailable => true;

        public string ConnectionType => "Ethernet";
    }

    private sealed class RecordingProbe : INetworkProbe
    {
        public List<(string Host, int Port)> Calls { get; } = [];

        public Task<OperationResult<int>> ProbeAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            Calls.Add((host, port));
            return Task.FromResult(OperationResult<int>.Success(12));
        }
    }
}

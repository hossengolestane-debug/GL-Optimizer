using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;
using GLOptimizer.Core.Settings;
using GLOptimizer.GameLoop;
using Microsoft.Data.Sqlite;

namespace GLOptimizer.Tests;

public class MarketAndCodDiagnosticsTests
{
    [Theory]
    [InlineData("1.2.3.4", "1.2.3.4")]
    [InlineData("\"1.0.0\"", "1.0.0")]
    [InlineData("v1.2", "1.2")]
    [InlineData("V1.10", "1.10")]
    public void PackageVersion_parses_unambiguous_text(string raw, string expected)
    {
        var version = PackageVersion.Parse(raw);
        Assert.NotNull(version);
        Assert.Equal(expected, version.Text);
    }

    [Theory]
    [InlineData("1.2.3-beta")]
    [InlineData("1.2.3-rc.1")]
    [InlineData("latest")]
    [InlineData("1")]
    [InlineData("V10.0.1 extra")]
    [InlineData("")]
    [InlineData(null)]
    public void PackageVersion_rejects_prerelease_and_garbage(string? raw)
    {
        Assert.Null(PackageVersion.Parse(raw));
    }

    [Fact]
    public void PackageVersion_compares_numeric_parts_and_pads_missing_zeros()
    {
        var left = PackageVersion.Parse("1.2");
        var same = PackageVersion.Parse("1.2.0");
        var higher = PackageVersion.Parse("1.10");
        var lower = PackageVersion.Parse("1.9");
        Assert.NotNull(left);
        Assert.NotNull(same);
        Assert.NotNull(higher);
        Assert.NotNull(lower);
        Assert.Equal(0, left.CompareTo(same));
        Assert.True(higher.CompareTo(lower) > 0);
    }

    [Theory]
    [InlineData("1.2.0", "1.2", null, CatalogComparison.Match, "Installed and market versions match.")]
    [InlineData("1.2", "1.2.0", "1.2.0", CatalogComparison.Match, "Installed, market, and official versions match.")]
    [InlineData("1.0.0", "2.0.0", null, CatalogComparison.VersionMismatch, "COD Mobile version mismatch: installed 1.0.0, market 2.0.0")]
    [InlineData("1.0.0", "2.0.0", "9.0.0", CatalogComparison.VersionMismatch, "COD Mobile version mismatch: installed 1.0.0, market 2.0.0")]
    [InlineData("1.2", "1.2", "1.3", CatalogComparison.LocalMarketOutdated, "Local market version 1.2 is older than official version 1.3.")]
    [InlineData("1.2", "1.2", "1.0", CatalogComparison.Unknown, CatalogComparisonLogic.CannotDistinguish)]
    [InlineData("1.2.3-beta", "1.2.3", null, CatalogComparison.Unknown, "Installed version is not an unambiguous version string.")]
    [InlineData("1.2.3", "1.2.3-rc.1", null, CatalogComparison.Unknown, "Market version is not an unambiguous version string.")]
    [InlineData(null, "1.2", null, CatalogComparison.Unknown, "A local version is missing.")]
    [InlineData("1.2", null, "1.2", CatalogComparison.Unknown, "A local version is missing.")]
    [InlineData("latest", "latest", null, CatalogComparison.Unknown, "Installed version is not an unambiguous version string.")]
    [InlineData("1", "1.0", null, CatalogComparison.Unknown, "Installed version is not an unambiguous version string.")]
    [InlineData("V10.0.1 extra", "10.0.1", null, CatalogComparison.Unknown, "Installed version is not an unambiguous version string.")]
    public void Comparison_matrix_never_declares_a_remote_catalog_issue(
        string? installed,
        string? market,
        string? official,
        CatalogComparison expected,
        string detail)
    {
        var result = CatalogComparisonLogic.Evaluate(installed, market, official);
        Assert.Equal(expected, result.Comparison);
        Assert.Equal(detail, result.Detail);
        Assert.NotEqual(CatalogComparison.RemoteCatalogIssue, result.Comparison);
        Assert.Equal(
            "Server-side GameLoop catalog issue detected. This cannot safely be modified locally.",
            CatalogComparisonLogic.RemoteMessage);
    }

    [Fact]
    public void Classifier_uses_known_names_and_states_the_layout_assumption()
    {
        var executable = AppMarketClassifier.Classify("AppMarket.exe", isDirectory: false);
        var package = AppMarketClassifier.Classify("ui/Android/data/com.activision.callofduty.shooter", isDirectory: true);
        var version = AppMarketClassifier.Classify("ui/Android/data/com.garena.game.codm/version.txt", isDirectory: false);
        var cache = AppMarketClassifier.Classify("AppMarket/cache", isDirectory: true);
        var metadata = AppMarketClassifier.Classify("AppMarket/catalog.db", isDirectory: false);
        var unknown = AppMarketClassifier.Classify("AppMarket/notes.txt", isDirectory: false);
        var ignored = AppMarketClassifier.Classify("readme.txt", isDirectory: false);
        var cacheFile = AppMarketClassifier.Classify("AppMarket/cache/blob.dat", isDirectory: false);
        var market3 = AppMarketClassifier.Classify("AppMarket3/apklocalpkgs.json", isDirectory: false);
        var market3Cache = AppMarketClassifier.Classify("AppMarket3/cache", isDirectory: true);

        Assert.NotNull(executable);
        Assert.Equal(MarketItemKind.Package, executable.Kind);
        Assert.Equal(MarketConfidence.High, executable.Confidence);
        Assert.NotNull(package);
        Assert.Equal(MarketItemKind.Package, package.Kind);
        Assert.Equal(MarketConfidence.High, package.Confidence);
        Assert.NotNull(version);
        Assert.Equal(MarketItemKind.Metadata, version.Kind);
        Assert.Equal(MarketConfidence.High, version.Confidence);
        Assert.NotNull(cache);
        Assert.Equal(MarketItemKind.Cache, cache.Kind);
        Assert.Equal(MarketConfidence.Medium, cache.Confidence);
        Assert.NotNull(metadata);
        Assert.Equal(MarketItemKind.Metadata, metadata.Kind);
        Assert.Equal(MarketConfidence.Medium, metadata.Confidence);
        Assert.NotNull(unknown);
        Assert.Equal(MarketItemKind.Unknown, unknown.Kind);
        Assert.Equal(MarketConfidence.Low, unknown.Confidence);
        Assert.Null(ignored);
        Assert.Null(cacheFile);
        Assert.NotNull(market3);
        Assert.Equal(MarketItemKind.Metadata, market3.Kind);
        Assert.NotNull(market3Cache);
        Assert.Equal(MarketItemKind.Cache, market3Cache.Kind);
        foreach (var item in new[] { executable, package, version, cache, metadata, unknown })
        {
            Assert.Contains("assumption", item.Reason, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Scan_stops_at_the_entry_and_depth_limits()
    {
        var root = NewTemp();
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "AppMarket", "cache"));
            File.WriteAllText(Path.Combine(root, "AppMarket", "later.txt"), "later");
            File.WriteAllText(Path.Combine(root, "AppMarket", "cache", "keep.txt"), "keep");
            var detector = new AppMarketDetector();
            var limited = detector.Scan(root, maxDepth: 6, maxEntries: 2, CancellationToken.None);
            Assert.True(limited.Truncated);
            Assert.InRange(limited.Visited, 1, 2);
            Assert.DoesNotContain(limited.Items, item => item.RelativePath.Replace('\\', '/').Contains("keep.txt", StringComparison.Ordinal));
            Assert.DoesNotContain(limited.Items, item => item.RelativePath.Replace('\\', '/').Contains("later.txt", StringComparison.Ordinal));

            var shallow = detector.Scan(root, maxDepth: 0, maxEntries: 400, CancellationToken.None);
            Assert.True(shallow.Truncated);
            Assert.Empty(shallow.Items);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Scan_does_not_follow_a_reparse_point_outside_the_install()
    {
        var root = NewTemp();
        var outside = NewTemp();
        try
        {
            File.WriteAllText(Path.Combine(outside, "secret.txt"), "secret");
            Directory.CreateSymbolicLink(Path.Combine(root, "AppMarket"), outside);
            var scan = new AppMarketDetector().Scan(root, maxDepth: 6, maxEntries: 400, CancellationToken.None);
            Assert.DoesNotContain(scan.Items, item => item.Path.StartsWith(outside, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(scan.Items, item => item.RelativePath.Contains("secret.txt", StringComparison.Ordinal));
            Assert.Equal("secret", File.ReadAllText(Path.Combine(outside, "secret.txt")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public void Cache_inventory_does_not_delete_files()
    {
        var root = NewTemp();
        try
        {
            var cache = Path.Combine(root, "AppMarket", "cache");
            Directory.CreateDirectory(cache);
            var file = Path.Combine(cache, "a.txt");
            File.WriteAllText(file, "keep");
            var items = new MarketInventoryItem[]
            {
                Item(cache, "AppMarket/cache", MarketItemKind.Cache, isDirectory: true),
                Item(file, "AppMarket/cache/a.txt", MarketItemKind.Unknown, isDirectory: false)
            };
            var listed = new AppMarketCacheManager().ListCaches(items);
            Assert.Single(listed);
            Assert.Equal(MarketItemKind.Cache, listed[0].Kind);
            Assert.Equal("keep", File.ReadAllText(file));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Sqlite_metadata_yields_one_unambiguous_market_version()
    {
        var root = NewTemp();
        try
        {
            var database = Path.Combine(root, "AppMarket", "com.activision.callofduty.shooter.db");
            Directory.CreateDirectory(Path.GetDirectoryName(database)!);
            WriteDatabase(database, "CREATE TABLE meta (versionName TEXT); INSERT INTO meta (versionName) VALUES ('1.2.3');");
            var version = new AppMarketVersionService().ReadMarketVersion(root, [Item(database, "AppMarket/com.activision.callofduty.shooter.db", MarketItemKind.Metadata, false)], CancellationToken.None);
            Assert.Equal("1.2.3", version);

            WriteDatabase(database, "CREATE TABLE meta (versionName TEXT); INSERT INTO meta (versionName) VALUES ('1.2.3'); INSERT INTO meta (versionName) VALUES ('1.2.4');");
            var conflict = new AppMarketVersionService().ReadMarketVersion(root, [Item(database, "AppMarket/com.activision.callofduty.shooter.db", MarketItemKind.Metadata, false)], CancellationToken.None);
            Assert.Null(conflict);

            WriteDatabase(database, "CREATE TABLE meta (versionName TEXT); INSERT INTO meta (versionName) VALUES ('1.2.3-beta');");
            var prerelease = new AppMarketVersionService().ReadMarketVersion(root, [Item(database, "AppMarket/com.activision.callofduty.shooter.db", MarketItemKind.Metadata, false)], CancellationToken.None);
            Assert.Null(prerelease);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Gray_screen_findings_are_ranked_from_evidence_without_a_cause_or_a_probability()
    {
        var findings = GrayScreenAnalyzer.Rank(new GrayScreenEvidence
        {
            Comparison = CatalogComparison.VersionMismatch,
            ComparisonDetail = "COD Mobile version mismatch: installed 1.0.0, market 2.0.0",
            LogFileFound = true,
            LogErrorLines = ["engine error: surface"],
            GameLoopRunning = false,
            EngineProcessRunning = false,
            CodProcessRunning = false,
            ProcessListDefinitive = true,
            WindowProbeSucceeded = false,
            EngineCpuSampled = false,
            SystemGpuSampled = false,
            CacheStale = true,
            CacheDetail = "Cache last write is earlier than the package.",
            Renderer = "OpenGL"
        });

        Assert.Equal("COD Mobile version mismatch", findings[0].Title);
        Assert.Equal(FindingOutcome.Failed, findings[0].Outcome);
        Assert.Equal(100, findings[0].Rank);
        Assert.Contains("App Market page", findings[0].RecommendedAction, StringComparison.Ordinal);
        Assert.Equal("Engine log lines", findings[1].Title);
        Assert.Equal(80, findings[1].Rank);
        Assert.Equal("GameLoop process", findings[2].Title);
        Assert.Equal(70, findings[2].Rank);
        Assert.Equal("Local cache timestamp", findings[3].Title);
        Assert.Equal(30, findings[3].Rank);
        var text = string.Join('\n', findings.Select(finding => finding.Title + " " + finding.Evidence + " " + finding.RecommendedAction));
        Assert.DoesNotContain("probability", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("likely", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("caused", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Missing_cpu_sample_does_not_claim_the_engine_was_idle()
    {
        var findings = GrayScreenAnalyzer.Rank(new GrayScreenEvidence
        {
            Comparison = CatalogComparison.Unknown,
            ComparisonDetail = "A local version is missing.",
            EngineProcessRunning = true,
            GameLoopRunning = true,
            CodProcessRunning = false,
            ProcessListDefinitive = true,
            WindowProbeSucceeded = false,
            EngineCpuSampled = false
        });
        var cpu = Assert.Single(findings, finding => finding.Title == "Engine CPU sample");
        Assert.Equal(FindingOutcome.Unknown, cpu.Outcome);
        Assert.DoesNotContain("0", cpu.Evidence, StringComparison.Ordinal);
        var surface = Assert.Single(findings, finding => finding.Title == "COD Mobile process or window");
        Assert.Equal(FindingOutcome.Warning, surface.Outcome);
        Assert.Contains("does not mean that no window exists", surface.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Official_version_is_not_requested_unless_check_version_is_used()
    {
        var source = new RecordingSource();
        var log = new ListLog();
        var diagnostics = Diagnostics(source, log);
        var local = await diagnostics.ScanAsync(checkOfficialVersion: false);
        Assert.Equal(OperationStatus.Success, local.Status);
        Assert.Equal(0, source.Calls);
        Assert.Empty(log.Messages);
        Assert.False(local.Value!.OfficialRequested);

        var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var stopped = await diagnostics.ScanAsync(checkOfficialVersion: true, cancelled.Token);
        Assert.Equal(OperationStatus.Failed, stopped.Status);
        Assert.Equal(0, source.Calls);

        source.Host = "updates.example.test";
        var remote = await diagnostics.ScanAsync(checkOfficialVersion: true);
        Assert.Equal(1, source.Calls);
        Assert.Contains(log.Messages, message => message.Contains("updates.example.test", StringComparison.Ordinal));

        var unavailable = await new UnavailableOfficialVersionSource().TryGetAsync("com.activision.callofduty.shooter");
        Assert.False(unavailable.RequestedNetwork);
        Assert.Null(unavailable.Host);
        Assert.Null(unavailable.Version);
        Assert.Equal("Official version source not implemented.", unavailable.Detail);
    }

    private static AppMarketDiagnostics Diagnostics(RecordingSource source, ListLog log) => new(
        new FixedDetector(),
        new AppMarketDetector(),
        new AppMarketVersionService(),
        new AppMarketCacheManager(),
        new CodMobileVersionChecker(),
        source,
        log,
        new FixedClock());

    private static MarketInventoryItem Item(string path, string relative, MarketItemKind kind, bool isDirectory) => new()
    {
        Path = path,
        RelativePath = relative,
        IsDirectory = isDirectory,
        Kind = kind,
        Confidence = MarketConfidence.Medium,
        Reason = "fixture"
    };

    private static void WriteDatabase(string path, string sql)
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Pooling = false
        }.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string NewTemp()
    {
        var path = Path.Combine(Path.GetTempPath(), "glopt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class FixedDetector : IGameLoopDetector
    {
        public Task<OperationResult<GameLoopScan>> DetectAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult<GameLoopScan>.Success(new GameLoopScan()));
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class RecordingSource : IOfficialVersionSource
    {
        public int Calls { get; private set; }

        public string? Host { get; set; }

        public Task<OfficialVersionResult> TryGetAsync(string? packageId, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(new OfficialVersionResult
            {
                Host = Host,
                RequestedNetwork = Host is not null,
                Detail = Host is null ? "Official version source not implemented." : "contacted"
            });
        }
    }

    private sealed class ListLog : ILogStore
    {
        public List<string> Messages { get; } = [];

        public string LogDirectory => string.Empty;

        public string ActiveLogFilePath => string.Empty;

        public LogSeverity MinimumLevel => LogSeverity.Information;

        public string? LastError => null;

        public void ApplyPolicy(AppSettings settings)
        {
        }

        public void Write(LogSeverity severity, string category, string message, Exception? exception = null) =>
            Messages.Add(message);

        public IReadOnlyList<LogEntry> GetRecent(int count = 200) => [];

        public IReadOnlyList<LogEntry> ReadActiveLog(int maxLines = 500) => [];

        public void Flush()
        {
        }
    }
}

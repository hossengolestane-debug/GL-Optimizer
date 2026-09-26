using System.Globalization;
using System.Text;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Monitoring;

namespace GLOptimizer.GameLoop;

public sealed class CodMobileLaunchDiagnostics
{
    private static readonly string[][] LogCandidates =
    [
        ["ui", "log.txt"],
        ["log", "aow_exe.log"],
        ["Engine", "log.txt"],
        ["AppMarket", "log.txt"]
    ];

    private readonly IGameLoopEnvironment _environment;
    private readonly IWindowTitleSource _windows;
    private readonly ISystemMonitor _system;
    private readonly IGameLoopMonitor _gameLoop;
    private readonly ISampleDelay _delay;
    private readonly IGameLoopConfigDiscovery _config;
    private readonly IClock _clock;

    public CodMobileLaunchDiagnostics(
        IGameLoopEnvironment environment,
        IWindowTitleSource windows,
        ISystemMonitor system,
        IGameLoopMonitor gameLoop,
        ISampleDelay delay,
        IGameLoopConfigDiscovery config,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(gameLoop);
        ArgumentNullException.ThrowIfNull(delay);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(clock);
        _environment = environment;
        _windows = windows;
        _system = system;
        _gameLoop = gameLoop;
        _delay = delay;
        _config = config;
        _clock = clock;
    }

    public async Task<GrayScreenEvidence> CollectAsync(
        IReadOnlyList<string> installPaths,
        IReadOnlyList<MarketInventoryItem> inventory,
        string? packagePath,
        CatalogComparison comparison,
        string? comparisonDetail,
        CancellationToken cancellationToken)
    {
        var roots = Normalize(installPaths);
        var processes = SafeProcesses();
        var windows = _windows.List();
        var probeSucceeded = windows.Succeeded && windows.Value is not null;
        var codWindow = probeSucceeded && WindowMatches(windows.Value!, processes, roots);
        var (cpuSampled, cpu, gpuSampled, gpu) = await SampleAsync(cancellationToken).ConfigureAwait(false);
        var renderer = await ReadRendererAsync(roots, cancellationToken).ConfigureAwait(false);
        var (cacheStale, cacheDetail) = Cache(inventory, packagePath);
        var (logFound, logLines) = Logs(roots);

        return new GrayScreenEvidence
        {
            GameLoopRunning = ProcessState(processes, roots, IsGameLoopProcess),
            EngineProcessRunning = ProcessState(processes, roots, IsEngineProcess),
            CodProcessRunning = ProcessState(processes, roots, IsCodProcess),
            CodWindowSeen = probeSucceeded ? codWindow : null,
            ProcessListDefinitive = processes.Available && !processes.HadUnreadableMatch,
            WindowProbeSucceeded = probeSucceeded,
            EngineCpuSampled = cpuSampled,
            EngineCpuPercent = cpu,
            SystemGpuSampled = gpuSampled,
            SystemGpuPercent = gpu,
            Comparison = comparison,
            ComparisonDetail = comparisonDetail,
            Renderer = renderer,
            CacheStale = cacheStale,
            CacheDetail = cacheDetail,
            LogFileFound = logFound,
            LogErrorLines = logLines
        };
    }

    private async Task<(bool SampledCpu, double? Cpu, bool SampledGpu, double? Gpu)> SampleAsync(CancellationToken cancellationToken)
    {
        try
        {
            var firstGame = _gameLoop.Read(_clock.UtcNow);
            var firstSystem = _system.Read();
            await _delay.WaitAsync(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
            var secondGame = _gameLoop.Read(_clock.UtcNow);
            var secondSystem = _system.Read();
            var cpuSampled = firstGame.CpuPercent is not null && secondGame.CpuPercent is not null;
            var gpuSampled = firstSystem.GpuPercent is not null && secondSystem.GpuPercent is not null;
            return (cpuSampled, cpuSampled ? secondGame.CpuPercent : null, gpuSampled, gpuSampled ? secondSystem.GpuPercent : null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return (false, null, false, null);
        }
    }

    private async Task<string?> ReadRendererAsync(IReadOnlyList<string> roots, CancellationToken cancellationToken)
    {
        if (roots.Count == 0)
        {
            return null;
        }

        try
        {
            var report = await _config.DiscoverAsync(roots, cancellationToken).ConfigureAwait(false);
            if (!report.Succeeded || report.Value is null)
            {
                return null;
            }

            var values = new HashSet<string>(StringComparer.Ordinal);
            foreach (var install in report.Value.Installs)
            {
                if (!string.IsNullOrWhiteSpace(install.Settings.Renderer))
                {
                    values.Add(install.Settings.Renderer);
                }
            }

            if (!string.IsNullOrWhiteSpace(report.Value.SharedSettings?.Renderer))
            {
                values.Add(report.Value.SharedSettings.Renderer);
            }

            return values.Count == 1 ? values.First() : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private ProcessQueryResult SafeProcesses()
    {
        try
        {
            return _environment.QueryProcesses();
        }
        catch (Exception)
        {
            return new ProcessQueryResult();
        }
    }

    private static bool? ProcessState(ProcessQueryResult query, IReadOnlyList<string> roots, Func<ProcessObservation, string, bool> match)
    {
        var saw = false;
        var unreadable = query.HadUnreadableMatch;
        foreach (var process in query.Processes)
        {
            if (string.IsNullOrWhiteSpace(process.ExecutablePath))
            {
                continue;
            }

            var path = process.ExecutablePath;
            if (!UnderAny(path, roots))
            {
                continue;
            }

            if (match(process, path))
            {
                saw = true;
            }
        }

        if (saw)
        {
            return true;
        }

        if (query.Available && !unreadable)
        {
            return false;
        }

        return null;
    }

    private static bool WindowMatches(IReadOnlyList<WindowTitle> titles, ProcessQueryResult query, IReadOnlyList<string> roots)
    {
        foreach (var title in titles)
        {
            if (!IsCodTitle(title.Title))
            {
                continue;
            }

            foreach (var process in query.Processes)
            {
                if (process.ProcessId != title.ProcessId || string.IsNullOrWhiteSpace(process.ExecutablePath))
                {
                    continue;
                }

                if (UnderAny(process.ExecutablePath, roots))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static (bool? Stale, string? Detail) Cache(IReadOnlyList<MarketInventoryItem> inventory, string? packagePath)
    {
        DateTimeOffset? cache = null;
        DateTimeOffset? package = null;
        foreach (var item in inventory)
        {
            if (item.Kind == MarketItemKind.Cache && item.LastWriteTimeUtc is DateTimeOffset cacheTime)
            {
                if (cache is null || cacheTime > cache)
                {
                    cache = cacheTime;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(packagePath))
        {
            package = LastWrite(packagePath);
        }

        if (package is null)
        {
            foreach (var item in inventory)
            {
                if (item.IsDirectory && item.Kind == MarketItemKind.Package && item.LastWriteTimeUtc is DateTimeOffset packageTime)
                {
                    if (package is null || packageTime > package)
                    {
                        package = packageTime;
                    }
                }
            }
        }

        if (cache is null || package is null)
        {
            return (null, "Cache and package last-write times were not both available.");
        }

        var stale = cache.Value < package.Value;
        var detail = "Cache last write " + cache.Value.ToString("u", CultureInfo.InvariantCulture)
            + (stale ? " is earlier than package last write " : " is not earlier than package last write ")
            + package.Value.ToString("u", CultureInfo.InvariantCulture) + ".";
        return (stale, detail);
    }

    private static (bool Found, IReadOnlyList<string> Lines) Logs(IReadOnlyList<string> roots)
    {
        var lines = new List<string>();
        var found = false;
        foreach (var root in roots)
        {
            foreach (var segments in LogCandidates)
            {
                var path = GameLoopLayout.Combine(root, segments);
                if (!File.Exists(path) || !InstallPathRules.IsUnderRoot(path, root) || IsReparse(path))
                {
                    continue;
                }

                found = true;
                foreach (var line in ReadInterestingLines(path))
                {
                    lines.Add(line);
                    if (lines.Count == 8)
                    {
                        return (true, lines);
                    }
                }
            }
        }

        return (found, lines);
    }

    private static IReadOnlyList<string> ReadInterestingLines(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var take = (int)Math.Min(stream.Length, 65536);
            if (take == 0)
            {
                return [];
            }

            stream.Seek(stream.Length - take, SeekOrigin.Begin);
            var buffer = new byte[take];
            stream.ReadExactly(buffer);
            var text = Encoding.UTF8.GetString(buffer);
            var hits = new List<string>();
            foreach (var raw in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                if (!raw.Contains("error", StringComparison.OrdinalIgnoreCase)
                    && !raw.Contains("fail", StringComparison.OrdinalIgnoreCase)
                    && !raw.Contains("exception", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var line = raw.Trim();
                if (line.Length > 240)
                {
                    line = line[..240];
                }

                hits.Add(line);
                if (hits.Count == 8)
                {
                    break;
                }
            }

            return hits;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }

    private static bool IsGameLoopProcess(ProcessObservation process, string _) =>
        process.ProcessName.Equals("GameLoop", StringComparison.OrdinalIgnoreCase)
        || process.ProcessName.Equals("TxGameAssistant", StringComparison.OrdinalIgnoreCase);

    private static bool IsEngineProcess(ProcessObservation process, string _) =>
        process.ProcessName.Equals("aow_exe", StringComparison.OrdinalIgnoreCase)
        || process.ProcessName.Equals("AndroidEmulator", StringComparison.OrdinalIgnoreCase)
        || process.ProcessName.Equals("AndroidEmulatorEn", StringComparison.OrdinalIgnoreCase);

    private static bool IsCodProcess(ProcessObservation process, string path)
    {
        if (process.ProcessName.Contains("codm", StringComparison.OrdinalIgnoreCase)
            || process.ProcessName.Contains("callofduty", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var packageId in MobilePackages.Cod)
        {
            if (path.Contains(packageId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCodTitle(string title) =>
        title.Contains("Call of Duty", StringComparison.OrdinalIgnoreCase)
        || title.Contains("COD Mobile", StringComparison.OrdinalIgnoreCase);

    private static bool UnderAny(string path, IReadOnlyList<string> roots)
    {
        foreach (var root in roots)
        {
            if (InstallPathRules.IsUnderRoot(path, root))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> Normalize(IReadOnlyList<string> paths)
    {
        var roots = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            var full = InstallPathRules.TryNormalize(path);
            if (full is not null && seen.Add(full))
            {
                roots.Add(full);
            }
        }

        return roots;
    }

    private static DateTimeOffset? LastWrite(string path)
    {
        try
        {
            if (!Directory.Exists(path) && !File.Exists(path))
            {
                return null;
            }

            var written = File.GetLastWriteTimeUtc(path);
            return written.Year < 1980
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(written, DateTimeKind.Utc));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static bool IsReparse(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return true;
        }
    }
}

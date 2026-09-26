using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Backup;
using GLOptimizer.Core.Configuration;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Models;

namespace GLOptimizer.GameLoop;

public sealed class GameLoopBackupSource : IBackupSource
{
    private readonly IGameLoopDetector _detector;
    private readonly IGameLoopConfigDiscovery _discovery;
    private readonly IGameLoopConfigReader _reader;

    public GameLoopBackupSource(IGameLoopDetector detector, IGameLoopConfigDiscovery discovery, IGameLoopConfigReader reader)
    {
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(reader);
        _detector = detector;
        _discovery = discovery;
        _reader = reader;
    }

    public async Task<BackupSourceSnapshot> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var scan = await _detector.DetectAsync(cancellationToken).ConfigureAwait(false);
        if (!scan.Succeeded || scan.Value is null)
        {
            return BackupSourceSnapshot.Failed(scan.Error ?? "GameLoop could not be scanned.");
        }

        var roots = GameLoopLocations.InstallAndData(scan.Value).ToList();

        var config = await _discovery.DiscoverAsync(roots, cancellationToken).ConfigureAwait(false);
        if (!config.Succeeded || config.Value is null)
        {
            return BackupSourceSnapshot.Failed(config.Error ?? "GameLoop configuration could not be read.");
        }

        var userDirectories = UserDirectories(_reader.KnownUserFiles());
        var files = new List<BackupSourceFile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in config.Value.Installs.SelectMany(install => install.Files).Concat(config.Value.SharedFiles))
        {
            if (record.Presence != ConfigPresence.Present || record.Kind == ConfigFileKind.Registry)
            {
                continue;
            }

            var normalized = InstallPathRules.TryNormalize(record.Path);
            if (normalized is null || !seen.Add(normalized))
            {
                continue;
            }

            if (!BackupPathRules.IsAllowedTarget(normalized, roots, userDirectories))
            {
                continue;
            }

            files.Add(new BackupSourceFile { Path = normalized });
        }

        var registry = _reader.ReadMobileGamePc();
        return new BackupSourceSnapshot
        {
            InstallRoots = roots,
            UserDirectories = userDirectories,
            Files = files,
            Settings = ChooseSettings(config.Value),
            GameLoopVersion = ChooseVersion(scan.Value.Installations),
            GameLoopRunning = scan.Value.Installations.Any(installation => installation.RunStatus == GameRunStatus.Running),
            RegistryValues = registry.Found
                ? registry.Values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            UnmappedRegistryKeys = registry.UnmappedRendererKeys.ToArray()
        };
    }

    private static GameLoopSettings ChooseSettings(GameLoopConfigReport report)
    {
        if (report.Installs.Count == 1)
        {
            return report.Installs[0].Settings;
        }

        return report.SharedSettings ?? new GameLoopSettings();
    }

    private static string? ChooseVersion(IReadOnlyList<GameLoopInstallation> installations)
    {
        string? version = null;
        foreach (var installation in installations)
        {
            if (string.IsNullOrWhiteSpace(installation.Version))
            {
                continue;
            }

            if (version is null)
            {
                version = installation.Version;
                continue;
            }

            if (!string.Equals(version, installation.Version, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return version;
    }

    private static IReadOnlyList<string> UserDirectories(IReadOnlyList<string> files)
    {
        var dirs = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var directory = Path.GetDirectoryName(file);
            var normalized = InstallPathRules.TryNormalize(directory);
            if (normalized is not null && seen.Add(normalized))
            {
                dirs.Add(normalized);
            }
        }

        return dirs;
    }
}

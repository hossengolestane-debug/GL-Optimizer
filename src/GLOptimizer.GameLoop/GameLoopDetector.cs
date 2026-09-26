using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.GameLoop;

public sealed class GameLoopDetector : IGameLoopDetector
{
    public const int PackageSearchDepth = 8;
    public const int PackageSearchNodes = 2500;
    private const int ParentHops = 4;

    private readonly IGameLoopEnvironment _environment;
    private readonly ISettingsStore? _settings;

    public GameLoopDetector(IGameLoopEnvironment environment)
        : this(environment, null)
    {
    }

    public GameLoopDetector(IGameLoopEnvironment environment, ISettingsStore? settings)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
        _settings = settings;
    }

    public async Task<OperationResult<GameLoopScan>> DetectAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return OperationResult<GameLoopScan>.Failure("The scan was cancelled.");
        }

        if (DeveloperSimulation.IsAvailable && _settings?.Current.DeveloperSimulationEnabled == true)
        {
            var simulated = DeveloperSimulation.Scan();
            if (simulated is not null)
            {
                return OperationResult<GameLoopScan>.Success(simulated);
            }
        }

        try
        {
            var scan = await Task.Run(() => Scan(cancellationToken), cancellationToken).ConfigureAwait(false);
            return OperationResult<GameLoopScan>.Success(scan);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<GameLoopScan>.Failure("The scan was cancelled.");
        }
        catch (Exception)
        {
            return OperationResult<GameLoopScan>.Failure("GameLoop could not be scanned.");
        }
    }

    private GameLoopScan Scan(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var warnings = new List<string>();
        var hints = ReadHints(warnings);
        var processes = ReadProcesses(warnings);
        var brokenHint = false;
        var candidates = CollectCandidates(hints, processes, ref brokenHint);

        var verified = new Dictionary<string, VerifiedInstall>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var match = Verify(candidate);
            if (match is null)
            {
                continue;
            }

            verified.TryAdd(match.Root, match);
        }

        var installations = new List<GameLoopInstallation>();
        foreach (var match in verified.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            installations.Add(BuildInstallation(match, hints, processes, warnings));
        }

        MobileGamePresence pubg;
        MobileGamePresence cod;
        if (installations.Count == 0)
        {
            pubg = UnknownGame("GameLoop was not found, so PUBG Mobile was not searched.");
            cod = UnknownGame("GameLoop was not found, so COD Mobile was not searched.");
        }
        else
        {
            var roots = new List<string>();
            foreach (var installation in installations)
            {
                if (!string.IsNullOrWhiteSpace(installation.InstallPath))
                {
                    roots.Add(installation.InstallPath);
                }
            }

            foreach (var extra in _environment.MobileDataRoots())
            {
                if (_environment.DirectoryExists(extra))
                {
                    roots.Add(extra);
                }
            }

            pubg = FindGame(MobilePackages.Pubg, roots, "PUBG Mobile", cancellationToken, warnings);
            cod = FindGame(MobilePackages.Cod, roots, "COD Mobile", cancellationToken, warnings);
        }

        return new GameLoopScan
        {
            Installations = installations,
            PubgMobile = pubg,
            CodMobile = cod,
            BrokenRegistration = brokenHint && installations.Count == 0,
            Warnings = warnings
        };
    }

    private IReadOnlyList<UninstallHint> ReadHints(List<string> warnings)
    {
        try
        {
            return _environment.ReadUninstallHints();
        }
        catch (Exception)
        {
            warnings.Add("Uninstall entries could not be read.");
            return [];
        }
    }

    private ProcessQueryResult ReadProcesses(List<string> warnings)
    {
        try
        {
            return _environment.QueryProcesses();
        }
        catch (Exception)
        {
            warnings.Add("Process list could not be read.");
            return new ProcessQueryResult();
        }
    }

    private List<string> CollectCandidates(IReadOnlyList<UninstallHint> hints, ProcessQueryResult processes, ref bool brokenHint)
    {
        var candidates = new List<string>();
        foreach (var hint in hints)
        {
            if (!GameLoopNames.IsProduct(hint.DisplayName))
            {
                continue;
            }

            var rooted = false;
            foreach (var raw in new[] { hint.InstallLocation, hint.DisplayIcon })
            {
                var normalized = InstallPathRules.TryNormalize(raw);
                if (normalized is null)
                {
                    continue;
                }

                if (_environment.FileExists(normalized) || _environment.DirectoryExists(normalized))
                {
                    candidates.Add(normalized);
                    rooted = true;
                }
            }

            if (!rooted)
            {
                brokenHint = true;
            }
        }

        AddRange(candidates, SafeList(_environment.CandidateDirectories));
        AddRange(candidates, SafeList(_environment.StartMenuTargets));
        if (processes.Available)
        {
            foreach (var process in processes.Processes)
            {
                if (!string.IsNullOrWhiteSpace(process.ExecutablePath))
                {
                    candidates.Add(process.ExecutablePath);
                }
            }
        }

        return candidates;
    }

    private IReadOnlyList<string> SafeList(Func<IReadOnlyList<string>> read)
    {
        try
        {
            return read();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static void AddRange(List<string> target, IReadOnlyList<string> source)
    {
        foreach (var item in source)
        {
            if (!string.IsNullOrWhiteSpace(item))
            {
                target.Add(item);
            }
        }
    }

    private VerifiedInstall? Verify(string candidate)
    {
        var current = InstallPathRules.TryNormalize(candidate);
        if (current is null)
        {
            return null;
        }

        if (_environment.FileExists(current))
        {
            current = InstallPathRules.TryNormalize(Path.GetDirectoryName(current));
        }

        if (current is null || !_environment.DirectoryExists(current))
        {
            return null;
        }

        for (var hop = 0; hop <= ParentHops && current is not null; hop++)
        {
            var launcher = FindLauncher(current);
            if (launcher is not null)
            {
                return new VerifiedInstall(current, launcher);
            }

            var parent = Path.GetDirectoryName(Trim(current));
            var next = InstallPathRules.TryNormalize(parent);
            if (next is null || !_environment.DirectoryExists(next) || string.Equals(next, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = next;
        }

        return null;
    }

    private string? FindLauncher(string root)
    {
        foreach (var segments in GameLoopLayout.LauncherSegments)
        {
            var path = GameLoopLayout.Combine(root, segments);
            if (_environment.FileExists(path))
            {
                return path;
            }
        }

        return null;
    }

    private GameLoopInstallation BuildInstallation(
        VerifiedInstall match,
        IReadOnlyList<UninstallHint> hints,
        ProcessQueryResult processes,
        List<string> warnings)
    {
        var (status, listed) = ResolveProcesses(match.Root, processes);
        return new GameLoopInstallation
        {
            InstallPath = match.Root,
            Version = ResolveVersion(match.Root, match.Launcher, hints, warnings),
            Engine = ResolveEngine(match.Root),
            RunStatus = status,
            LauncherPath = match.Launcher,
            Processes = listed
        };
    }

    private string? ResolveVersion(string root, string launcher, IReadOnlyList<UninstallHint> hints, List<string> warnings)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var hint in hints)
        {
            if (!GameLoopNames.IsProduct(hint.DisplayName) || !HintApplies(hint, root))
            {
                continue;
            }

            var version = VersionText.Normalize(hint.DisplayVersion);
            if (version is not null)
            {
                found.Add(version);
            }
        }

        if (found.Count > 1)
        {
            warnings.Add("Conflicting GameLoop versions were reported for one install.");
            return null;
        }

        if (found.Count == 1)
        {
            return found.First();
        }

        return _environment.TryReadFileVersion(launcher);
    }

    private static bool HintApplies(UninstallHint hint, string root)
    {
        return InstallPathRules.IsUnderRoot(hint.InstallLocation, root)
            || InstallPathRules.IsUnderRoot(hint.DisplayIcon, root);
    }

    private string? ResolveEngine(string root)
    {
        foreach (var segments in GameLoopLayout.EngineSegments)
        {
            if (_environment.FileExists(GameLoopLayout.Combine(root, segments)))
            {
                return "AOW";
            }
        }

        return null;
    }

    private (GameRunStatus Status, IReadOnlyList<GameLoopProcessInfo> Processes) ResolveProcesses(string root, ProcessQueryResult query)
    {
        if (!query.Available)
        {
            return (GameRunStatus.Unknown, []);
        }

        var listed = new List<GameLoopProcessInfo>();
        foreach (var process in query.Processes)
        {
            if (string.IsNullOrWhiteSpace(process.ExecutablePath))
            {
                continue;
            }

            if (!InstallPathRules.IsUnderRoot(process.ExecutablePath, root))
            {
                continue;
            }

            var normalized = InstallPathRules.TryNormalize(process.ExecutablePath);
            if (normalized is null)
            {
                continue;
            }

            listed.Add(new GameLoopProcessInfo
            {
                ProcessId = process.ProcessId,
                ProcessName = string.IsNullOrWhiteSpace(process.ProcessName) ? "Unknown" : process.ProcessName,
                ExecutablePath = normalized
            });
        }

        if (listed.Count > 0)
        {
            return (GameRunStatus.Running, listed);
        }

        return query.HadUnreadableMatch
            ? (GameRunStatus.Unknown, [])
            : (GameRunStatus.Stopped, []);
    }

    private MobileGamePresence FindGame(
        IReadOnlyList<string> packageIds,
        IReadOnlyList<string> roots,
        string label,
        CancellationToken cancellationToken,
        List<string> warnings)
    {
        var matches = new List<string>();
        var completed = true;
        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var packageId in packageIds)
            {
                foreach (var parent in GameLoopLayout.PackageParents)
                {
                    var segments = new string[parent.Length + 1];
                    parent.CopyTo(segments, 0);
                    segments[^1] = packageId;
                    var path = GameLoopLayout.Combine(root, segments);
                    if (_environment.DirectoryExists(path))
                    {
                        matches.Add(path);
                    }
                }
            }

            DirectorySearchResult search;
            try
            {
                search = _environment.FindDirectoriesNamed(root, packageIds, PackageSearchDepth, PackageSearchNodes, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                completed = false;
                warnings.Add(label + " search could not be completed.");
                continue;
            }

            if (!search.Completed)
            {
                completed = false;
            }

            matches.AddRange(search.Matches);
        }

        var distinct = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var match in matches)
        {
            if (seen.Add(match))
            {
                distinct.Add(match);
            }
        }

        if (distinct.Count == 0)
        {
            if (!completed)
            {
                warnings.Add(label + " search stopped early.");
                return UnknownGame(label + " could not be fully searched.");
            }

            return new MobileGamePresence
            {
                Status = GamePresenceStatus.NotFound,
                Detail = label + " was not found under the verified GameLoop data."
            };
        }

        var versions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var directory in distinct)
        {
            var version = ReadPackageVersion(directory);
            if (version is not null)
            {
                versions.Add(version);
            }
        }

        string? chosen = versions.Count == 1 ? versions.First() : null;
        if (versions.Count > 1)
        {
            warnings.Add(label + " reported conflicting versions.");
        }

        return new MobileGamePresence
        {
            Status = GamePresenceStatus.Installed,
            Version = chosen,
            PackageId = PackageIdOf(distinct[0], packageIds),
            Path = distinct[0],
            Detail = chosen is null
                ? label + " is installed. Version unknown."
                : label + " " + chosen
        };
    }

    private string? ReadPackageVersion(string directory)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in GameLoopLayout.VersionFileNames)
        {
            var text = _environment.ReadSmallText(Path.Combine(directory, name), 65536);
            var version = VersionText.FromContent(text);
            if (version is not null)
            {
                found.Add(version);
            }
        }

        return found.Count == 1 ? found.First() : null;
    }

    private static string? PackageIdOf(string path, IReadOnlyList<string> packageIds)
    {
        var name = Path.GetFileName(Trim(path));
        foreach (var packageId in packageIds)
        {
            if (name.Equals(packageId, StringComparison.OrdinalIgnoreCase))
            {
                return packageId;
            }
        }

        return null;
    }

    private static MobileGamePresence UnknownGame(string detail) => new()
    {
        Status = GamePresenceStatus.Unknown,
        Detail = detail
    };

    private static string Trim(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private sealed record VerifiedInstall(string Root, string Launcher);
}

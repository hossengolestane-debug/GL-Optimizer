using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Configuration;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Results;

namespace GLOptimizer.GameLoop;

public sealed class GameLoopConfigDiscovery : IGameLoopConfigDiscovery
{
    private readonly IGameLoopConfigReader _reader;

    public GameLoopConfigDiscovery(IGameLoopConfigReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    public async Task<OperationResult<GameLoopConfigReport>> DiscoverAsync(
        IReadOnlyList<string> installPaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installPaths);
        if (cancellationToken.IsCancellationRequested)
        {
            return OperationResult<GameLoopConfigReport>.Failure("The scan was cancelled.");
        }

        try
        {
            var report = await Task.Run(() => Build(installPaths, cancellationToken), cancellationToken).ConfigureAwait(false);
            return OperationResult<GameLoopConfigReport>.Success(report);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<GameLoopConfigReport>.Failure("The scan was cancelled.");
        }
        catch (Exception)
        {
            return OperationResult<GameLoopConfigReport>.Failure("GameLoop configuration could not be read.");
        }
    }

    private GameLoopConfigReport Build(IReadOnlyList<string> installPaths, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var roots = new List<string>();
        var seenRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in installPaths)
        {
            var normalized = InstallPathRules.TryNormalize(path);
            if (normalized is not null && seenRoots.Add(normalized))
            {
                roots.Add(normalized);
            }
        }

        if (roots.Count == 0)
        {
            return new GameLoopConfigReport
            {
                Notice = "GameLoop was not found, so configuration was not searched."
            };
        }

        var installs = new List<InstallWork>();
        var dataRoots = new List<InstallWork>();
        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsGameLoopData(root))
            {
                dataRoots.Add(ReadData(root, cancellationToken));
            }
            else
            {
                installs.Add(ReadInstall(root, cancellationToken));
            }
        }

        var sharedFiles = new List<ConfigFileRecord>();
        var sharedSettings = new List<GameLoopSettings>();
        if (installs.Count == 1)
        {
            foreach (var data in dataRoots)
            {
                installs[0].Files.AddRange(data.Files);
                installs[0].Settings.AddRange(data.Settings);
            }
        }
        else
        {
            foreach (var data in dataRoots)
            {
                sharedFiles.AddRange(data.Files);
                sharedSettings.AddRange(data.Settings);
            }
        }

        var userPresent = ReadShared(sharedFiles, sharedSettings, cancellationToken);
        if (installs.Count == 0 && (sharedFiles.Count > 0 || dataRoots.Count > 0))
        {
            return new GameLoopConfigReport
            {
                SharedFiles = sharedFiles,
                SharedSettings = sharedSettings.Count == 0 ? null : GameLoopSettingsParser.Merge(sharedSettings),
                Notice = "GameLoop data was read without a verified install directory."
            };
        }

        if (installs.Count == 1)
        {
            var install = installs[0];
            install.Files.AddRange(sharedFiles);
            install.Settings.Add(GameLoopSettingsParser.Merge(sharedSettings));
            return new GameLoopConfigReport
            {
                Installs =
                [
                    new InstallConfigReport
                    {
                        InstallPath = install.Path,
                        Settings = GameLoopSettingsParser.Merge(install.Settings),
                        Files = install.Files
                    }
                ]
            };
        }

        return new GameLoopConfigReport
        {
            Installs = installs.Select(install => new InstallConfigReport
            {
                InstallPath = install.Path,
                Settings = GameLoopSettingsParser.Merge(install.Settings),
                Files = install.Files
            }).ToArray(),
            SharedFiles = sharedFiles,
            SharedSettings = sharedSettings.Count == 0 ? null : GameLoopSettingsParser.Merge(sharedSettings),
            Notice = userPresent
                ? "User configuration was not applied to one install because more than one GameLoop install is present."
                : null
        };
    }

    private InstallWork ReadInstall(string root, CancellationToken cancellationToken)
    {
        var work = new InstallWork(root);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in GameLoopConfigCatalog.InstallRelative)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var full = Path.GetFullPath(GameLoopLayout.Combine(root, candidate.Segments));
            if (!InstallPathRules.IsUnderRoot(full, root) || !seen.Add(full))
            {
                continue;
            }

            var readText = candidate.Kind == ConfigFileKind.EngineSettings;
            var probe = _reader.ProbeFile(full, readText);
            work.Files.Add(Describe(full, candidate.Kind, probe, readText, out var settings));
            if (settings is not null)
            {
                work.Settings.Add(settings);
            }
        }

        return work;
    }

    private InstallWork ReadData(string root, CancellationToken cancellationToken)
    {
        var work = new InstallWork(root);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in GameLoopConfigCatalog.DataRelative)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddProbed(work, seen, root, GameLoopLayout.Combine(root, candidate.Segments), candidate.Kind, readText: true, cancellationToken);
        }

        var configDir = Path.Combine(root, "app", "config");
        foreach (var path in _reader.ListSiblingConfigs(configDir))
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddProbed(work, seen, root, path, ConfigFileKind.EngineSettings, readText: true, cancellationToken);
        }

        return work;
    }

    private void AddProbed(
        InstallWork work,
        HashSet<string> seen,
        string root,
        string path,
        ConfigFileKind kind,
        bool readText,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string full;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return;
        }

        if (!InstallPathRules.IsUnderRoot(full, root) || !seen.Add(full))
        {
            return;
        }

        var probe = _reader.ProbeFile(full, readText);
        work.Files.Add(Describe(full, kind, probe, readText, out var settings));
        if (settings is not null)
        {
            work.Settings.Add(settings);
        }
    }

    private static bool IsGameLoopData(string root)
    {
        var name = Path.GetFileName(Trim(root));
        return name.Equals("GameLoopData", StringComparison.OrdinalIgnoreCase);
    }

    private static string Trim(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private bool ReadShared(List<ConfigFileRecord> files, List<GameLoopSettings> settings, CancellationToken cancellationToken)
    {
        var present = false;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in _reader.KnownUserFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = InstallPathRules.TryNormalize(path);
            if (normalized is null || !seen.Add(normalized))
            {
                continue;
            }

            var kind = IsKeyMap(normalized) ? ConfigFileKind.KeyMap : ConfigFileKind.UserSettings;
            var packageList = Path.GetFileName(normalized).Equals("apklocalpkgs.json", StringComparison.OrdinalIgnoreCase);
            var parse = kind == ConfigFileKind.UserSettings && !packageList;
            var probe = _reader.ProbeFile(normalized, parse);
            var record = Describe(normalized, kind, probe, parse, out var parsed, packageList ? "App Market package list. Contents were not mapped to settings." : null);
            files.Add(record);
            if (record.Presence == ConfigPresence.Present)
            {
                present = true;
            }

            if (parsed is not null)
            {
                settings.Add(parsed);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var registry = _reader.ReadMobileGamePc();
        files.Add(DescribeRegistry(registry, out var registrySettings));
        if (registry.Found)
        {
            present = true;
        }

        if (registrySettings is not null)
        {
            settings.Add(registrySettings);
        }

        foreach (var extra in ReadSupplemental())
        {
            cancellationToken.ThrowIfCancellationRequested();
            files.Add(DescribeSupplemental(extra));
            if (extra.Found)
            {
                present = true;
            }
        }

        return present;
    }

    private IReadOnlyList<SupplementalRegistry> ReadSupplemental()
    {
        try
        {
            return _reader.ReadSupplementalRegistries();
        }
        catch (Exception)
        {
            return
            [
                new SupplementalRegistry
                {
                    Path = "Supplemental registry",
                    Failed = true
                }
            ];
        }
    }

    private static ConfigFileRecord Describe(
        string path,
        ConfigFileKind kind,
        ConfigProbe probe,
        bool parsedContent,
        out GameLoopSettings? settings,
        string? presentDetail = null)
    {
        settings = null;
        if (!probe.Exists && !probe.ReadFailed)
        {
            return new ConfigFileRecord
            {
                Path = path,
                Kind = kind,
                Presence = ConfigPresence.NotFound,
                Detail = "Not found."
            };
        }

        if (probe.ReparsePoint)
        {
            return new ConfigFileRecord
            {
                Path = path,
                Kind = kind,
                Presence = ConfigPresence.Unreadable,
                SizeBytes = probe.SizeBytes,
                LastWriteTime = probe.LastWriteTime,
                Detail = "The path is a link and was not followed."
            };
        }

        if (probe.ReadFailed)
        {
            return new ConfigFileRecord
            {
                Path = path,
                Kind = kind,
                Presence = ConfigPresence.Unreadable,
                SizeBytes = probe.SizeBytes,
                LastWriteTime = probe.LastWriteTime,
                Detail = "The file could not be read."
            };
        }

        if (probe.TooLarge)
        {
            return new ConfigFileRecord
            {
                Path = path,
                Kind = kind,
                Presence = ConfigPresence.Unreadable,
                SizeBytes = probe.SizeBytes,
                LastWriteTime = probe.LastWriteTime,
                Detail = "Larger than 64 KB, so it was not parsed."
            };
        }

        if (!parsedContent || probe.Text is null)
        {
            return new ConfigFileRecord
            {
                Path = path,
                Kind = kind,
                Presence = ConfigPresence.Present,
                SizeBytes = probe.SizeBytes,
                LastWriteTime = probe.LastWriteTime,
                Detail = presentDetail ?? (kind == ConfigFileKind.KeyMap
                    ? "Key map file. Contents were not read as engine settings."
                    : "Present. The contents were not a recognized text config.")
            };
        }

        if (!GameLoopSettingsParser.TryReadPairs(probe.Text, Path.GetExtension(path), out var pairs))
        {
            return new ConfigFileRecord
            {
                Path = path,
                Kind = kind,
                Presence = ConfigPresence.Unreadable,
                SizeBytes = probe.SizeBytes,
                LastWriteTime = probe.LastWriteTime,
                Detail = "The file was not valid ini, conf, json, or xml."
            };
        }

        settings = GameLoopSettingsParser.Parse(pairs);
        var detail = "Parsed. Only known keys with valid values were mapped.";
        var unknown = GameLoopSettingsParser.UnrecognizedNames(pairs);
        if (unknown.Count > 0)
        {
            detail += " Unrecognized settings are read-only: " + string.Join(", ", unknown) + ".";
        }

        return new ConfigFileRecord
        {
            Path = path,
            Kind = kind,
            Presence = ConfigPresence.Present,
            SizeBytes = probe.SizeBytes,
            LastWriteTime = probe.LastWriteTime,
            Detail = detail
        };
    }

    private static ConfigFileRecord DescribeSupplemental(SupplementalRegistry probe)
    {
        if (probe.Failed && !probe.Found)
        {
            return new ConfigFileRecord
            {
                Path = probe.Path,
                Kind = ConfigFileKind.Registry,
                Presence = ConfigPresence.Unreadable,
                Detail = "The registry key could not be read."
            };
        }

        if (!probe.Found)
        {
            return new ConfigFileRecord
            {
                Path = probe.Path,
                Kind = ConfigFileKind.Registry,
                Presence = ConfigPresence.NotFound,
                Detail = "Not found."
            };
        }

        var names = probe.ValueNames.Take(24).ToArray();
        var detail = names.Length == 0
            ? "Unrecognized settings are read-only."
            : "Unrecognized settings are read-only: " + string.Join(", ", names) + ".";
        if (probe.Path.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase))
        {
            detail += " This key is not written.";
        }

        return new ConfigFileRecord
        {
            Path = probe.Path,
            Kind = ConfigFileKind.Registry,
            Presence = ConfigPresence.Present,
            Detail = detail
        };
    }

    private static ConfigFileRecord DescribeRegistry(RegistryProbe probe, out GameLoopSettings? settings)
    {
        settings = null;
        if (probe.Failed && !probe.Found)
        {
            return new ConfigFileRecord
            {
                Path = GameLoopConfigCatalog.RegistryPath,
                Kind = ConfigFileKind.Registry,
                Presence = ConfigPresence.Unreadable,
                Detail = "The registry key could not be read."
            };
        }

        if (!probe.Found)
        {
            return new ConfigFileRecord
            {
                Path = GameLoopConfigCatalog.RegistryPath,
                Kind = ConfigFileKind.Registry,
                Presence = ConfigPresence.NotFound,
                Detail = "Not found."
            };
        }

        var pairs = probe.Values.Select(pair => new ConfigPair(pair.Key, pair.Value)).ToArray();
        settings = GameLoopSettingsParser.Parse(pairs);
        var detail = "Read-only. Only known values were mapped.";
        if (probe.UnmappedRendererKeys.Count > 0)
        {
            detail += " Unmapped renderer values: " + string.Join(", ", probe.UnmappedRendererKeys) + ".";
        }

        return new ConfigFileRecord
        {
            Path = GameLoopConfigCatalog.RegistryPath,
            Kind = ConfigFileKind.Registry,
            Presence = ConfigPresence.Present,
            Detail = detail
        };
    }

    private static bool IsKeyMap(string path)
    {
        var name = Path.GetFileName(path);
        return name.Equals("TVM_100.xml", StringComparison.OrdinalIgnoreCase)
            || name.Contains("KeyMapping", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class InstallWork(string path)
    {
        public string Path { get; } = path;

        public List<ConfigFileRecord> Files { get; } = [];

        public List<GameLoopSettings> Settings { get; } = [];
    }
}

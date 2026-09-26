using System.Globalization;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Backup;
using GLOptimizer.Core.Configuration;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Optimization;
using GLOptimizer.Core.Results;

namespace GLOptimizer.GameLoop;

public sealed class OptimizationEngine : IOptimizationService
{
    private readonly IHardwareService _hardware;
    private readonly IGameLoopDetector _detector;
    private readonly IGameLoopConfigDiscovery _discovery;
    private readonly IGameLoopConfigReader _reader;
    private readonly IBackupService _backups;
    private readonly IHostOptimizationProbe _host;
    private readonly IConfigFileWriter _writer;
    private readonly IOptimizationRecordStore _records;
    private readonly IClock _clock;
    private readonly ILogStore _log;

    public OptimizationEngine(
        IHardwareService hardware,
        IGameLoopDetector detector,
        IGameLoopConfigDiscovery discovery,
        IGameLoopConfigReader reader,
        IBackupService backups,
        IHostOptimizationProbe host,
        IConfigFileWriter writer,
        IOptimizationRecordStore records,
        IClock clock,
        ILogStore log)
    {
        _hardware = hardware;
        _detector = detector;
        _discovery = discovery;
        _reader = reader;
        _backups = backups;
        _host = host;
        _writer = writer;
        _records = records;
        _clock = clock;
        _log = log;
    }

    public async Task<OperationResult<OptimizationAnalysis>> AnalyzeAsync(OptimizationProfile profile, CancellationToken cancellationToken = default)
    {
        try
        {
            var context = await LoadAsync(profile, cancellationToken).ConfigureAwait(false);
            if (context.Error is not null)
            {
                return OperationResult<OptimizationAnalysis>.Failure(context.Error);
            }

            return OperationResult<OptimizationAnalysis>.Success(context.Analysis!);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<OptimizationAnalysis>.Failure("The optimization analysis was cancelled.");
        }
        catch (Exception)
        {
            return OperationResult<OptimizationAnalysis>.Failure("Optimization could not be analyzed.");
        }
    }

    public async Task<OperationResult<OptimizationPreview>> PreviewAsync(OptimizationProfile profile, CancellationToken cancellationToken = default)
    {
        var analysis = await AnalyzeAsync(profile, cancellationToken).ConfigureAwait(false);
        if (!analysis.Succeeded || analysis.Value is null)
        {
            return OperationResult<OptimizationPreview>.Failure(analysis.Error ?? "The preview could not be built.");
        }

        return OperationResult<OptimizationPreview>.Success(ToPreview(analysis.Value));
    }

    public async Task<OperationResult<OptimizationReport>> ApplyAsync(OptimizationProfile profile, bool confirmed, CancellationToken cancellationToken = default)
    {
        if (!confirmed)
        {
            return OperationResult<OptimizationReport>.Failure("Optimization was not confirmed.");
        }

        string? backupId = null;
        try
        {
            var context = await LoadAsync(profile, cancellationToken).ConfigureAwait(false);
            if (context.Error is not null || context.Analysis is null)
            {
                return OperationResult<OptimizationReport>.Failure(context.Error ?? "Optimization could not be analyzed.");
            }

            var preview = ToPreview(context.Analysis);
            if (!preview.CanApply)
            {
                var message = preview.BlockReason ?? "There is no applicable change to apply.";
                _log.Write(LogSeverity.Warning, "Optimize", message);
                return OperationResult<OptimizationReport>.Failure(message);
            }

            var backup = await _backups.CreateAsync("Optimization apply (" + profile + ").", cancellationToken).ConfigureAwait(false);
            if (!backup.Succeeded || backup.Value?.Manifest is null)
            {
                var message = backup.Error ?? "The backup failed, so nothing was changed.";
                _log.Write(LogSeverity.Error, "Optimize", message);
                return OperationResult<OptimizationReport>.Failure(message);
            }

            backupId = backup.Value.Manifest.Id;
            var noted = _records.Save(new OptimizationUndoRecord
            {
                BackupId = backupId,
                Profile = profile.ToString(),
                AppliedAtUtc = _clock.UtcNow,
                InProgress = true
            });
            if (!noted.Succeeded)
            {
                return await FailAndRestore(backupId, "The undo record could not be saved, so the pre-apply backup was restored.", cancellationToken).ConfigureAwait(false);
            }

            foreach (var group in preview.Edits.GroupBy(edit => edit.Path, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = Path.GetFullPath(group.Key);
                if (!BackupPathRules.IsAllowedTarget(path, context.InstallRoots, context.UserDirectories)
                    || BackupPathRules.IsReparse(path)
                    || BackupPathRules.IsReparse(Path.GetDirectoryName(path)))
                {
                    return await FailAndRestore(backupId, "A target path is outside the allowlist, so the pre-apply backup was restored.", cancellationToken).ConfigureAwait(false);
                }

                var before = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
                var decoded = ConfigFileEncoding.Decode(before);
                if (!ConfigTextEditor.TryApply(decoded.Text, group.First().Format, group.ToArray(), out var updated, out var editError))
                {
                    return await FailAndRestore(backupId, editError ?? "A value could not be replaced, so the pre-apply backup was restored.", cancellationToken).ConfigureAwait(false);
                }

                var write = _writer.Write(path, ConfigFileEncoding.Encode(updated, decoded.Encoding, decoded.Bom));
                if (!write.Succeeded)
                {
                    return await FailAndRestore(backupId, write.Error ?? "A file could not be written, so the pre-apply backup was restored.", cancellationToken).ConfigureAwait(false);
                }

            }

            var validation = await ValidateAsync(context, preview.Edits, cancellationToken).ConfigureAwait(false);
            if (validation is not null)
            {
                return await FailAndRestore(backupId, validation, cancellationToken).ConfigureAwait(false);
            }

            var saved = _records.Save(new OptimizationUndoRecord
            {
                BackupId = backupId,
                Profile = profile.ToString(),
                AppliedAtUtc = _clock.UtcNow,
                InProgress = false
            });
            var report = BuildReport(context.Analysis, backupId);
            _log.Write(LogSeverity.Information, "Optimize", report.Summary + " Backup " + backupId + ".");
            if (!saved.Succeeded)
            {
                _log.Write(LogSeverity.Warning, "Optimize", saved.Error ?? "The undo record could not be saved.");
            }

            return OperationResult<OptimizationReport>.Success(report);
        }
        catch (OperationCanceledException)
        {
            return backupId is null
                ? OperationResult<OptimizationReport>.Failure("The optimization was cancelled.")
                : await FailAndRestore(backupId, "The optimization was cancelled, so the pre-apply backup was restored.", CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return backupId is null
                ? OperationResult<OptimizationReport>.Failure("Optimization could not be applied.")
                : await FailAndRestore(backupId, "Optimization could not be applied, so the pre-apply backup was restored.", CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async Task<OperationResult<RestoreReport>> UndoLastAsync(bool confirmed, CancellationToken cancellationToken = default)
    {
        if (!confirmed)
        {
            return OperationResult<RestoreReport>.Failure("Undo was not confirmed.");
        }

        var record = _records.Read();
        if (!record.Succeeded || record.Value is null || string.IsNullOrWhiteSpace(record.Value.BackupId))
        {
            return OperationResult<RestoreReport>.Failure(record.Error ?? "There is no optimization to undo.");
        }

        var context = await LoadAsync(OptimizationProfile.Balanced, cancellationToken).ConfigureAwait(false);
        if (context.Analysis?.GameLoopRunning == true)
        {
            return OperationResult<RestoreReport>.Failure("GameLoop is running. Close it before undoing. This action does not stop the process.");
        }

        var restored = await _backups.RestoreAsync(record.Value.BackupId, confirmed: true, cancellationToken).ConfigureAwait(false);
        if (!restored.Succeeded)
        {
            _log.Write(LogSeverity.Error, "Optimize", restored.Error ?? "Undo failed.");
            return restored;
        }

        _records.Clear();
        _log.Write(LogSeverity.Information, "Optimize", "Undid the last optimization with backup " + record.Value.BackupId + ".");
        return restored;
    }

    private async Task<OperationResult<OptimizationReport>> FailAndRestore(string backupId, string reason, CancellationToken cancellationToken)
    {
        var restored = await _backups.RestoreAsync(backupId, confirmed: true, cancellationToken).ConfigureAwait(false);
        if (restored.Succeeded)
        {
            _records.Clear();
        }

        var message = restored.Succeeded
            ? reason.Contains("restored", StringComparison.OrdinalIgnoreCase)
                ? reason
                : reason + " The pre-apply backup was restored."
            : reason + " The backup could not be restored. Backup " + backupId + ".";
        _log.Write(LogSeverity.Error, "Optimize", message);
        return OperationResult<OptimizationReport>.Failure(message);
    }

    private async Task<string?> ValidateAsync(LoadContext context, IReadOnlyList<OptimizationEdit> edits, CancellationToken cancellationToken)
    {
        var scan = await _detector.DetectAsync(cancellationToken).ConfigureAwait(false);
        if (!scan.Succeeded || scan.Value is null)
        {
            return "GameLoop could not be verified after the write.";
        }

        var roots = Roots(scan.Value);
        foreach (var path in edits.Select(edit => Path.GetFullPath(edit.Path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (context.InstallRoots.Any(root => InstallPathRules.IsUnderRoot(path, root))
                && !roots.Any(root => InstallPathRules.IsUnderRoot(path, root)))
            {
                return "The GameLoop install was not verified after the write.";
            }
        }

        var discovery = await _discovery.DiscoverAsync(roots, cancellationToken).ConfigureAwait(false);
        if (!discovery.Succeeded || discovery.Value is null)
        {
            return "Configuration could not be read back.";
        }

        var records = discovery.Value.Installs.SelectMany(install => install.Files).Concat(discovery.Value.SharedFiles).ToArray();
        foreach (var group in edits.GroupBy(edit => edit.Path, StringComparer.OrdinalIgnoreCase))
        {
            var path = Path.GetFullPath(group.Key);
            if (!records.Any(record => record.Presence == ConfigPresence.Present && PathsEqual(record.Path, path)))
            {
                return "A changed file was not found again after the write.";
            }

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            var text = ConfigFileEncoding.Decode(bytes).Text;
            if (!GameLoopSettingsParser.TryReadPairs(text, Path.GetExtension(path), out var pairs))
            {
                return "A changed file could not be parsed after the write.";
            }

            foreach (var edit in group)
            {
                var values = pairs.Where(pair => pair.Key.Equals(edit.Key, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Value).ToArray();
                if (values.Length == 0 || values.Any(value => !string.Equals(value?.Trim().Trim('"'), edit.NewRaw, StringComparison.Ordinal)))
                {
                    return "A changed key did not read back the expected value.";
                }
            }

            var edited = group.Select(edit => edit.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var beforeBytes = context.OriginalBytes[path];
            var beforeText = ConfigFileEncoding.Decode(beforeBytes).Text;
            if (!GameLoopSettingsParser.TryReadPairs(beforeText, Path.GetExtension(path), out var beforePairs))
            {
                return "The original file could not be compared.";
            }

            var left = beforePairs.Where(pair => !edited.Contains(pair.Key)).ToArray();
            var right = pairs.Where(pair => !edited.Contains(pair.Key)).ToArray();
            if (left.Length != right.Length)
            {
                return "An untouched key changed.";
            }

            for (var i = 0; i < left.Length; i++)
            {
                if (!left[i].Key.Equals(right[i].Key, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(left[i].Value, right[i].Value, StringComparison.Ordinal))
                {
                    return "An untouched key changed.";
                }
            }
        }

        return null;
    }

    private async Task<LoadContext> LoadAsync(OptimizationProfile profile, CancellationToken cancellationToken)
    {
        var hardware = await _hardware.GetReportAsync(cancellationToken).ConfigureAwait(false);
        if (!hardware.Succeeded)
        {
            return LoadContext.Failed(hardware.Error ?? "Hardware could not be read.");
        }

        var scan = await _detector.DetectAsync(cancellationToken).ConfigureAwait(false);
        if (!scan.Succeeded || scan.Value is null)
        {
            return LoadContext.Failed(scan.Error ?? "GameLoop could not be scanned.");
        }

        var roots = Roots(scan.Value);
        var discovery = await _discovery.DiscoverAsync(roots, cancellationToken).ConfigureAwait(false);
        if (!discovery.Succeeded || discovery.Value is null)
        {
            return LoadContext.Failed(discovery.Error ?? "GameLoop configuration could not be read.");
        }

        var userDirectories = UserDirectories(_reader.KnownUserFiles());
        var located = new List<LocatedSetting>();
        var originals = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in discovery.Value.Installs.SelectMany(install => install.Files).Concat(discovery.Value.SharedFiles))
        {
            if (record.Presence != ConfigPresence.Present || record.Kind is ConfigFileKind.KeyMap or ConfigFileKind.Registry)
            {
                continue;
            }

            var path = InstallPathRules.TryNormalize(record.Path);
            if (path is null
                || !BackupPathRules.IsAllowedTarget(path, roots, userDirectories)
                || BackupPathRules.IsReparse(path)
                || !File.Exists(path))
            {
                continue;
            }

            var info = new FileInfo(path);
            if (info.Length > GameLoopConfigCatalog.MaxTextBytes)
            {
                continue;
            }

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            var decoded = ConfigFileEncoding.Decode(bytes);
            var format = Path.GetExtension(path);
            if (!GameLoopSettingsParser.TryReadPairs(decoded.Text, format, out var pairs))
            {
                continue;
            }

            originals[path] = bytes;
            located.AddRange(SettingLocator.Locate(path, format.TrimStart('.'), pairs, fromRegistry: false));
        }

        var registry = _reader.ReadMobileGamePc();
        if (registry.Found)
        {
            var pairs = registry.Values.Select(pair => new ConfigPair(pair.Key, pair.Value)).ToArray();
            located.AddRange(SettingLocator.Locate(GameLoopConfigCatalog.RegistryPath, "registry", pairs, fromRegistry: true));
        }

        var running = scan.Value.Installations.Any(installation => installation.RunStatus == GameRunStatus.Running);
        var tier = HardwareTierClassifier.Classify(hardware.Value);
        var recommendations = OptimizationRules.Build(tier, profile, hardware.Value, located).ToList();
        recommendations.AddRange(_host.Detect());
        return new LoadContext
        {
            Analysis = new OptimizationAnalysis
            {
                Profile = profile,
                Tier = tier,
                GameLoopRunning = running,
                Notice = roots.Count == 0 ? "GameLoop was not found, so configuration was not changed." : null,
                Recommendations = recommendations
            },
            InstallRoots = roots,
            UserDirectories = userDirectories,
            OriginalBytes = originals
        };
    }

    private static OptimizationPreview ToPreview(OptimizationAnalysis analysis)
    {
        var edits = analysis.Recommendations.SelectMany(item => item.Edits).ToArray();
        string? block = null;
        if (analysis.GameLoopRunning)
        {
            block = "GameLoop is running. Close it before optimizing. This action does not stop the process.";
        }
        else if (edits.Length == 0)
        {
            block = "There is no applicable change for this profile.";
        }

        return new OptimizationPreview
        {
            Analysis = analysis,
            Edits = edits,
            CanApply = block is null,
            BlockReason = block
        };
    }

    private static OptimizationReport BuildReport(OptimizationAnalysis analysis, string backupId)
    {
        var applied = analysis.Recommendations.Count(item => item.Status == RecommendationStatus.Applicable);
        var optimal = analysis.Recommendations.Count(item => item.Status == RecommendationStatus.AlreadyOptimal);
        var skipped = analysis.Recommendations.Count(item => item.Status is RecommendationStatus.Skipped or RecommendationStatus.NotSupported or RecommendationStatus.NotImplemented);
        var summary = Count(applied, "change") + " applied, " + Count(optimal, "already optimal") + ", " + Count(skipped, "skipped") + ". Restart GameLoop required.";
        return new OptimizationReport
        {
            Applied = applied,
            AlreadyOptimal = optimal,
            Skipped = skipped,
            BackupId = backupId,
            RestartRequired = true,
            Summary = summary,
            ResultText = "GameLoop configuration updated. Monitor performance during your next session to compare results."
        };
    }

    private static string Count(int count, string word)
    {
        if (word == "already optimal")
        {
            return count.ToString(CultureInfo.InvariantCulture) + " already optimal";
        }

        return count.ToString(CultureInfo.InvariantCulture) + " " + word + (count == 1 ? string.Empty : "s");
    }

    private static List<string> Roots(GameLoopScan scan) => GameLoopLocations.InstallAndData(scan).ToList();

    private static List<string> UserDirectories(IReadOnlyList<string> files)
    {
        var dirs = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var normalized = InstallPathRules.TryNormalize(Path.GetDirectoryName(file));
            if (normalized is not null && seen.Add(normalized))
            {
                dirs.Add(normalized);
            }
        }

        return dirs;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private sealed class LoadContext
    {
        public string? Error { get; init; }

        public OptimizationAnalysis? Analysis { get; init; }

        public IReadOnlyList<string> InstallRoots { get; init; } = [];

        public IReadOnlyList<string> UserDirectories { get; init; } = [];

        public Dictionary<string, byte[]> OriginalBytes { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public static LoadContext Failed(string error) => new() { Error = error };
    }
}

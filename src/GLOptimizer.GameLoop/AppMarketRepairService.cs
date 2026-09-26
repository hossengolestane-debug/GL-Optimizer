using System.Globalization;
using System.Text.Json;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Backup;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Repair;
using GLOptimizer.Core.Results;
using GLOptimizer.Infrastructure;

namespace GLOptimizer.GameLoop;

public sealed class AppMarketRepairService : IAppMarketRepair
{
    public const string CompletedToast = "GameLoop App Market repair completed.";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly IAppMarketDiagnostics _market;
    private readonly IProcessControl _processes;
    private readonly IWindowTitleSource _windows;
    private readonly GameLoopSessionStopper _stopper;
    private readonly IRepairStateStore _checkpoint;
    private readonly ILogStore _log;
    private readonly IClock _clock;
    private readonly string _backupsRoot;

    public AppMarketRepairService(
        IAppMarketDiagnostics market,
        IProcessControl processes,
        IWindowTitleSource windows,
        GameLoopSessionStopper stopper,
        IRepairStateStore checkpoint,
        ILogStore log,
        IClock clock,
        AppDataLocations locations)
    {
        ArgumentNullException.ThrowIfNull(market);
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(windows);
        ArgumentNullException.ThrowIfNull(stopper);
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(locations);
        _market = market;
        _processes = processes;
        _windows = windows;
        _stopper = stopper;
        _checkpoint = checkpoint;
        _log = log;
        _clock = clock;
        _backupsRoot = locations.BackupsDirectory;
    }

    public async Task<OperationResult<AppMarketRepairPlan>> DryRunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var built = await BuildAsync(cancellationToken).ConfigureAwait(false);
            if (built.Error is not null || built.Script is null)
            {
                return OperationResult<AppMarketRepairPlan>.Failure(built.Error ?? "The dry run could not be completed.");
            }

            return OperationResult<AppMarketRepairPlan>.Success(ToPlan(built));
        }
        catch (OperationCanceledException)
        {
            return OperationResult<AppMarketRepairPlan>.Failure("The dry run was cancelled.");
        }
        catch (Exception)
        {
            return OperationResult<AppMarketRepairPlan>.Failure("The dry run could not be completed.");
        }
    }

    public async Task<OperationResult<AppMarketRepairResult>> RepairAsync(bool confirmed, bool forceConfirmed, CancellationToken cancellationToken = default)
    {
        if (!confirmed)
        {
            return OperationResult<AppMarketRepairResult>.Failure("Repair was not confirmed.");
        }

        string? directory = null;
        var checkpointOpen = false;
        try
        {
            var built = await BuildAsync(cancellationToken).ConfigureAwait(false);
            if (built.Error is not null || built.Assessment is null || built.Script is null)
            {
                return Failure(built.Error ?? "The repair could not be checked.");
            }

            if (!built.Assessment.CanRepair)
            {
                return Failure(built.Assessment.ReviewReason ?? "No verified cache was selected, so nothing was changed.");
            }

            if (!Recheck(built))
            {
                return Failure("A cache path failed re-validation and nothing was moved.");
            }

            var stopped = await _stopper.StopAsync(built.InstallRoots, forceConfirmed, cancellationToken).ConfigureAwait(false);
            if (stopped.NeedsForceConfirmation)
            {
                return OperationResult<AppMarketRepairResult>.Success(new AppMarketRepairResult
                {
                    NeedsForceConfirmation = true,
                    Message = stopped.Message,
                    Verdict = RepairVerdictKind.Failed
                });
            }

            if (!stopped.Stopped)
            {
                return Failure(stopped.Message);
            }

            var created = _clock.UtcNow;
            var id = BackupPathRules.NewId(created);
            directory = BackupPathRules.ResolveBackupDirectory(_backupsRoot, id);
            if (directory is null)
            {
                return Failure("The backup folder could not be created.");
            }

            Directory.CreateDirectory(directory);
            var metadata = new List<StoredRepairFile>();
            foreach (var item in built.MetadataFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var root = RootFor(built.InstallRoots, item);
                var copied = root is null ? null : QuarantineStore.CopyMetadata(directory, root, item);
                if (copied is null)
                {
                    throw new IOException("A metadata file could not be backed up, so cache was not cleared.");
                }

                metadata.Add(copied);
            }

            var movedFiles = new List<StoredRepairFile>();
            _checkpoint.Save(new RepairCheckpoint
            {
                BackupId = id,
                PreMarketVersion = built.MarketVersion,
                InstalledVersion = built.InstalledVersion,
                CompletedAtUtc = created,
                AwaitingRefresh = false,
                InProgress = true,
                InstallRoots = built.InstallRoots.ToList()
            });
            checkpointOpen = true;
            foreach (var root in built.InstallRoots)
            {
                var batch = new List<string>();
                foreach (var file in built.ClearFiles)
                {
                    if (InstallPathRules.IsUnderRoot(file, root) && RootFor(built.InstallRoots, file) == root)
                    {
                        batch.Add(file);
                    }
                }

                if (batch.Count == 0)
                {
                    continue;
                }

                var moved = QuarantineStore.Move(new QuarantineMoveRequest
                {
                    BackupDirectory = directory,
                    InstallRoot = root,
                    SourceFiles = batch,
                    SameVolume = SameVolume(root, directory)
                }, cancellationToken);
                if (!moved.Succeeded)
                {
                    foreach (var earlier in movedFiles)
                    {
                        _ = QuarantineStore.Restore(directory, earlier, built.InstallRoots, []);
                    }

                    if (moved.RolledBack)
                    {
                        Abandon(ref directory, ref checkpointOpen);
                    }

                    _log.Write(LogSeverity.Error, "App Market", moved.Error ?? "The repair did not finish.");
                    return Failure(moved.Error ?? RepairVerdictLogic.FailedMessage);
                }

                movedFiles.AddRange(moved.Moved);
            }

            if (movedFiles.Count == 0)
            {
                Abandon(ref directory, ref checkpointOpen);
                return Failure("No verified cache was selected, so nothing was changed.");
            }

            var manifest = new BackupManifest
            {
                Id = id,
                CreatedAtUtc = created,
                CreatedAtLocal = created.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture),
                Reason = "App Market repair",
                Settings = new(),
                Quarantine = movedFiles,
                MetadataCopies = metadata
            };
            await File.WriteAllTextAsync(
                Path.Combine(directory, BackupPathRules.ManifestFileName),
                JsonSerializer.Serialize(manifest, JsonOptions),
                cancellationToken).ConfigureAwait(false);
            _checkpoint.Save(new RepairCheckpoint
            {
                BackupId = id,
                PreMarketVersion = built.MarketVersion,
                InstalledVersion = built.InstalledVersion,
                CompletedAtUtc = created,
                AwaitingRefresh = true,
                InProgress = false,
                InstallRoots = built.InstallRoots.ToList()
            });
            checkpointOpen = false;
            directory = null;
            _log.Write(LogSeverity.Information, "App Market", CompletedToast);
            var verdict = RepairVerdictLogic.Evaluate(true, false, built.MarketVersion, null, built.InstalledVersion);
            return OperationResult<AppMarketRepairResult>.Success(new AppMarketRepairResult
            {
                Completed = true,
                AwaitingRefresh = true,
                BackupId = id,
                Verdict = verdict.Kind,
                Message = stopped.Message + " " + verdict.Message,
                Toast = CompletedToast
            });
        }
        catch (OperationCanceledException)
        {
            Abandon(ref directory, ref checkpointOpen);
            return Failure("The repair was cancelled and nothing was left moved.");
        }
        catch (Exception ex)
        {
            Abandon(ref directory, ref checkpointOpen);
            _log.Write(LogSeverity.Error, "App Market", ex.Message);
            return Failure(RepairVerdictLogic.FailedMessage);
        }
    }

    public async Task<OperationResult<AppMarketRepairResult>> RecheckAsync(CancellationToken cancellationToken = default)
    {
        var checkpoint = _checkpoint.Load();
        if (checkpoint is null || !checkpoint.AwaitingRefresh)
        {
            return Failure("No completed repair is waiting for a re-check.");
        }

        var scan = await _market.ScanAsync(checkOfficialVersion: false, cancellationToken).ConfigureAwait(false);
        if (!scan.Succeeded || scan.Value is null)
        {
            return Failure(scan.Error ?? "The market could not be read again.");
        }

        var verdict = RepairVerdictLogic.Evaluate(
            true,
            true,
            checkpoint.PreMarketVersion,
            scan.Value.MarketVersion,
            scan.Value.InstalledVersion ?? checkpoint.InstalledVersion);
        checkpoint.AwaitingRefresh = verdict.Kind == RepairVerdictKind.AwaitingRefresh;
        _checkpoint.Save(checkpoint);
        _log.Write(LogSeverity.Information, "App Market", verdict.Message);
        return OperationResult<AppMarketRepairResult>.Success(new AppMarketRepairResult
        {
            Completed = false,
            AwaitingRefresh = checkpoint.AwaitingRefresh,
            BackupId = checkpoint.BackupId,
            Verdict = verdict.Kind,
            Message = verdict.Message
        });
    }

    public Task<OperationResult<AppMarketRepairResult>> RollbackInterruptedAsync(bool confirmed, CancellationToken cancellationToken = default)
    {
        if (!confirmed)
        {
            return Task.FromResult(Failure("Rollback was not confirmed."));
        }

        try
        {
            var checkpoint = _checkpoint.Load();
            if (!RepairRecovery.NeedsRollback(checkpoint) || checkpoint is null)
            {
                return Task.FromResult(Failure("No interrupted repair is waiting."));
            }

            var directory = BackupPathRules.ResolveBackupDirectory(_backupsRoot, checkpoint.BackupId);
            if (directory is null || !Directory.Exists(directory))
            {
                _checkpoint.Clear();
                return Task.FromResult(Rolled("The interrupted repair folder is gone, so nothing was restored.", checkpoint.BackupId));
            }

            var quarantine = Path.Combine(directory, QuarantineStore.FolderName);
            if (!Directory.Exists(quarantine))
            {
                _checkpoint.Clear();
                return Task.FromResult(Rolled("No quarantined files remained.", checkpoint.BackupId));
            }

            if (IsReparse(quarantine))
            {
                return Task.FromResult(Failure("The quarantine folder is a link and was not restored."));
            }

            foreach (var file in Directory.EnumerateFiles(quarantine, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsReparse(file))
                {
                    return Task.FromResult(Failure("A quarantined path is a link and was not restored."));
                }

                var relative = Path.GetRelativePath(quarantine, file);
                var original = RepairRecovery.OriginalPath(checkpoint.InstallRoots, relative);
                if (original is null)
                {
                    return Task.FromResult(Failure("A quarantined path is outside the saved install and was not restored."));
                }

                var error = QuarantineStore.Restore(directory, new StoredRepairFile
                {
                    OriginalPath = original,
                    StoredRelativePath = relative.Replace('\\', '/'),
                    Sha256 = QuarantineStore.HashFile(file),
                    SizeBytes = new FileInfo(file).Length
                }, checkpoint.InstallRoots, []);
                if (error is not null)
                {
                    _log.Write(LogSeverity.Error, "App Market", error);
                    return Task.FromResult(Failure(error));
                }
            }

            _checkpoint.Clear();
            const string message = "The interrupted repair was rolled back from quarantine.";
            _log.Write(LogSeverity.Information, "App Market", message);
            return Task.FromResult(Rolled(message, checkpoint.BackupId));
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(Failure("The rollback was cancelled."));
        }
        catch (Exception)
        {
            return Task.FromResult(Failure("The interrupted repair could not be rolled back."));
        }
    }

    private async Task<BuiltPlan> BuildAsync(CancellationToken cancellationToken)
    {
        var scan = await _market.ScanAsync(checkOfficialVersion: false, cancellationToken).ConfigureAwait(false);
        if (!scan.Succeeded || scan.Value is null)
        {
            return new BuiltPlan { Error = scan.Error ?? "App Market could not be scanned." };
        }

        var report = scan.Value;
        if (report.InstallPaths.Count == 0)
        {
            return new BuiltPlan { Error = "GameLoop was not found, so App Market cache was not searched." };
        }

        var candidates = new List<RepairCandidate>();
        var clearFiles = new List<string>();
        var metadataFiles = new List<string>();
        foreach (var root in report.InstallPaths)
        {
            foreach (var item in report.Inventory)
            {
                if (!InstallPathRules.IsUnderRoot(item.Path, root))
                {
                    continue;
                }

                if (item.Kind == MarketItemKind.Metadata && !item.IsDirectory)
                {
                    candidates.Add(Describe(item, root, item.SizeBytes, 1));
                    if (!AppMarketRepairRules.IsProtected(item.RelativePath) && !IsReparse(item.Path))
                    {
                        metadataFiles.Add(item.Path);
                    }

                    continue;
                }

                if (item.Kind != MarketItemKind.Cache)
                {
                    continue;
                }

                var files = ListCacheFiles(item.Path);
                if (files.Error is not null)
                {
                    candidates.Add(Describe(item, root, 0, 1, reparse: true));
                    continue;
                }

                long bytes = 0;
                foreach (var file in files.Files)
                {
                    bytes += SafeLength(file);
                    var relative = Path.GetRelativePath(root, file);
                    if (AppMarketRepairRules.IsProtected(relative))
                    {
                        candidates.Add(new RepairCandidate
                        {
                            FullPath = file,
                            RelativePath = relative,
                            Kind = MarketItemKind.Cache,
                            SizeBytes = SafeLength(file),
                            FileCount = 1,
                            UnderVerifiedRoot = true
                        });
                    }
                }

                if (files.Files.Count == 0 && !files.Reparse)
                {
                    continue;
                }

                candidates.Add(Describe(item, root, bytes, Math.Max(files.Files.Count, files.Reparse ? 1 : 0), files.Reparse));
                if (!files.Reparse)
                {
                    foreach (var file in files.Files)
                    {
                        if (!AppMarketRepairRules.IsProtected(Path.GetRelativePath(root, file)))
                        {
                            clearFiles.Add(file);
                        }
                    }
                }
            }
        }

        var assessment = AppMarketRepairRules.Assess(candidates);
        var processes = _processes.List();
        var titles = WindowTitles();
        var stop = ProcessSelection.SelectStopTargets(processes, report.InstallPaths);
        var session = ProcessSelection.GameSessionAppearsActive(processes, titles, report.InstallPaths);
        var script = new RepairScript
        {
            StopPaths = stop.Select(process => process.ProcessName + " " + process.ExecutablePath).ToArray(),
            SessionWarning = session ? ProcessSelection.SessionWarning : null,
            BackupPaths = assessment.Backup.Select(item => item.FullPath).ToArray(),
            ClearPaths = assessment.CanRepair
                ? clearFiles
                : assessment.Clear.Select(item => item.FullPath).Concat(assessment.Unexpected).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            NeedsReview = assessment.NeedsReview,
            ReviewReason = assessment.ReviewReason
        };
        return new BuiltPlan
        {
            Assessment = assessment,
            Script = script,
            InstallRoots = report.InstallPaths,
            ClearFiles = assessment.CanRepair ? clearFiles : [],
            MetadataFiles = assessment.CanRepair ? metadataFiles : [],
            MarketVersion = report.MarketVersion,
            InstalledVersion = report.InstalledVersion
        };
    }

    private static bool Recheck(BuiltPlan built)
    {
        foreach (var file in built.ClearFiles)
        {
            if (!File.Exists(file) || IsReparse(file))
            {
                return false;
            }

            var under = false;
            foreach (var root in built.InstallRoots)
            {
                if (InstallPathRules.IsUnderRoot(file, root) && QuarantineStore.RelativeUnder(root, file) is not null)
                {
                    under = true;
                }
            }

            if (!under || AppMarketRepairRules.IsProtected(file))
            {
                return false;
            }
        }

        return built.ClearFiles.Count > 0;
    }

    private IReadOnlyList<string> WindowTitles()
    {
        var listed = _windows.List();
        if (!listed.Succeeded || listed.Value is null)
        {
            return [];
        }

        return listed.Value.Select(window => window.Title).ToArray();
    }

    private static AppMarketRepairPlan ToPlan(BuiltPlan built) => new()
    {
        Text = built.Script!.Format(),
        CanRepair = built.Assessment!.CanRepair,
        NeedsReview = built.Assessment.NeedsReview,
        ReviewReason = built.Assessment.ReviewReason,
        ClearPaths = built.Script.ClearPaths,
        BackupPaths = built.Script.BackupPaths,
        StopPaths = built.Script.StopPaths
    };

    private static RepairCandidate Describe(MarketInventoryItem item, string root, long bytes, int files, bool reparse = false) => new()
    {
        FullPath = item.Path,
        RelativePath = item.RelativePath,
        Kind = item.Kind,
        IsDirectory = item.IsDirectory,
        SizeBytes = bytes,
        FileCount = files,
        IsReparse = reparse || IsReparse(item.Path),
        UnderVerifiedRoot = InstallPathRules.IsUnderRoot(item.Path, root)
    };

    private static CacheFiles ListCacheFiles(string path)
    {
        if (IsReparse(path))
        {
            return new CacheFiles { Reparse = true, Error = "reparse" };
        }

        if (File.Exists(path))
        {
            return new CacheFiles { Files = [path] };
        }

        if (!Directory.Exists(path))
        {
            return new CacheFiles { Error = "missing" };
        }

        var files = new List<string>();
        var pending = new Stack<string>();
        pending.Push(path);
        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            if (IsReparse(directory))
            {
                return new CacheFiles { Reparse = true, Error = "reparse" };
            }

            try
            {
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    if (IsReparse(file))
                    {
                        return new CacheFiles { Reparse = true, Error = "reparse" };
                    }

                    files.Add(file);
                    if (files.Count > AppMarketRepairRules.MaxFiles)
                    {
                        return new CacheFiles { Files = files };
                    }
                }

                foreach (var child in Directory.EnumerateDirectories(directory))
                {
                    pending.Push(child);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new CacheFiles { Error = "unreadable" };
            }
        }

        return new CacheFiles { Files = files };
    }

    private static string? RootFor(IReadOnlyList<string> roots, string path)
    {
        foreach (var root in roots)
        {
            if (InstallPathRules.IsUnderRoot(path, root))
            {
                return root;
            }
        }

        return null;
    }

    private static bool SameVolume(string left, string right)
    {
        try
        {
            return string.Equals(Path.GetPathRoot(Path.GetFullPath(left)), Path.GetPathRoot(Path.GetFullPath(right)), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static long SafeLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static bool IsReparse(string path)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return false;
            }

            return File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception)
        {
            return true;
        }
    }

    private void Abandon(ref string? directory, ref bool checkpointOpen)
    {
        if (!TryDelete(directory))
        {
            return;
        }

        directory = null;
        if (!checkpointOpen)
        {
            return;
        }

        _checkpoint.Clear();
        checkpointOpen = false;
    }

    private static bool TryDelete(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return true;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
            return !Directory.Exists(directory);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static OperationResult<AppMarketRepairResult> Rolled(string message, string backupId) =>
        OperationResult<AppMarketRepairResult>.Success(new AppMarketRepairResult
        {
            Completed = false,
            Message = message,
            Verdict = RepairVerdictKind.Failed,
            BackupId = backupId
        });

    private static OperationResult<AppMarketRepairResult> Failure(string message) =>
        OperationResult<AppMarketRepairResult>.Failure(message);

    private sealed class BuiltPlan
    {
        public string? Error { get; init; }

        public RepairAssessment? Assessment { get; init; }

        public RepairScript? Script { get; init; }

        public IReadOnlyList<string> InstallRoots { get; init; } = [];

        public IReadOnlyList<string> ClearFiles { get; init; } = [];

        public IReadOnlyList<string> MetadataFiles { get; init; } = [];

        public string? MarketVersion { get; init; }

        public string? InstalledVersion { get; init; }
    }

    private sealed class CacheFiles
    {
        public IReadOnlyList<string> Files { get; init; } = [];

        public bool Reparse { get; init; }

        public string? Error { get; init; }
    }
}

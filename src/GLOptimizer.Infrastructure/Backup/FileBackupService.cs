using System.Globalization;
using System.Text.Json;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Backup;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Infrastructure.Backup;

public sealed class FileBackupService : IBackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly IBackupSource _source;
    private readonly IClock _clock;
    private readonly ILogStore _log;
    private readonly string _backupsRoot;

    public FileBackupService(IBackupSource source, IClock clock, ILogStore log, AppDataLocations locations)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(locations);
        _source = source;
        _clock = clock;
        _log = log;
        _backupsRoot = locations.BackupsDirectory;
    }

    public OperationResult<IReadOnlyList<BackupRecord>> List()
    {
        try
        {
            return OperationResult<IReadOnlyList<BackupRecord>>.Success(ReadRecords());
        }
        catch (Exception ex)
        {
            return Fail<IReadOnlyList<BackupRecord>>(ex);
        }
    }

    public OperationResult<BackupDetails> Get(string backupId)
    {
        try
        {
            var loaded = Load(backupId);
            return loaded is null
                ? OperationResult<BackupDetails>.Failure("That backup was not found.")
                : OperationResult<BackupDetails>.Success(loaded);
        }
        catch (Exception ex)
        {
            return Fail<BackupDetails>(ex);
        }
    }

    public Task<OperationResult<BackupDetails>> CreateAsync(string reason, CancellationToken cancellationToken = default)
    {
        var normalized = BackupPathRules.NormalizeReason(reason);
        if (normalized is null)
        {
            return Task.FromResult(OperationResult<BackupDetails>.Failure(
                "Enter a reason of 1 to 200 characters, without line breaks."));
        }

        return CreateCoreAsync(normalized, cancellationToken);
    }

    public async Task<OperationResult<RestorePreview>> PreviewRestoreAsync(string backupId, CancellationToken cancellationToken = default)
    {
        try
        {
            var loaded = Load(backupId);
            if (loaded is null)
            {
                return OperationResult<RestorePreview>.Failure("That backup was not found.");
            }

            if (loaded.Damaged || loaded.Manifest is null)
            {
                return OperationResult<RestorePreview>.Failure(loaded.Problem ?? "This backup is damaged and cannot be restored.");
            }

            var source = await _source.CaptureAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(source.Error))
            {
                return OperationResult<RestorePreview>.Failure(source.Error!);
            }

            var preview = Plan(loaded.Manifest, source);
            return OperationResult<RestorePreview>.Success(preview);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<RestorePreview>.Failure("The restore preview was cancelled.");
        }
        catch (Exception ex)
        {
            return Fail<RestorePreview>(ex);
        }
    }

    public async Task<OperationResult<RestoreReport>> RestoreAsync(string backupId, bool confirmed, CancellationToken cancellationToken = default)
    {
        if (!confirmed)
        {
            return OperationResult<RestoreReport>.Failure("Restore was not confirmed.");
        }

        try
        {
            var preview = await PreviewRestoreAsync(backupId, cancellationToken).ConfigureAwait(false);
            if (!preview.Succeeded || preview.Value is null)
            {
                return OperationResult<RestoreReport>.Failure(preview.Error ?? "The backup could not be restored.");
            }

            if (!preview.Value.CanRestore)
            {
                var message = preview.Value.Warning ?? "Restore was refused because one or more files are not safe to replace.";
                _log.Write(LogSeverity.Warning, "Backup", "Restore refused for " + backupId + ". " + message);
                return OperationResult<RestoreReport>.Failure(message);
            }

            string? preId = null;
            if (preview.Value.Files.Any(file => file.Disposition == RestoreDisposition.Replace && File.Exists(file.OriginalPath)))
            {
                var pre = await CreateCoreAsync("Pre-restore backup before restoring " + backupId + ".", cancellationToken).ConfigureAwait(false);
                if (!pre.Succeeded || pre.Value?.Manifest is null)
                {
                    var message = pre.Error ?? "A pre-restore backup could not be created, so nothing was replaced.";
                    _log.Write(LogSeverity.Error, "Backup", message);
                    return OperationResult<RestoreReport>.Failure(message);
                }

                preId = pre.Value.Manifest.Id;
            }

            var written = 0;
            var unchanged = 0;
            foreach (var file in preview.Value.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (file.Disposition == RestoreDisposition.Unchanged)
                {
                    unchanged++;
                    continue;
                }

                if (file.Disposition != RestoreDisposition.Replace)
                {
                    return OperationResult<RestoreReport>.Failure("Restore stopped because a file was no longer safe to replace.");
                }

                var directory = BackupPathRules.ResolveBackupDirectory(_backupsRoot, backupId);
                var stored = directory is null ? null : BackupPathRules.ResolveStoredFile(directory, file.StoredName);
                var parent = Path.GetDirectoryName(file.OriginalPath);
                if (stored is null
                    || parent is null
                    || BackupPathRules.IsReparse(stored)
                    || BackupPathRules.IsReparse(parent)
                    || BackupPathRules.IsReparse(file.OriginalPath))
                {
                    return OperationResult<RestoreReport>.Failure("Restore stopped because a path was no longer safe.");
                }

                var bytes = ReadLimited(stored);
                if (!string.Equals(BackupPathRules.Sha256(bytes), file.BackupSha256, StringComparison.OrdinalIgnoreCase))
                {
                    return OperationResult<RestoreReport>.Failure("Restore stopped because a backup copy no longer matches its manifest hash.");
                }

                WriteAtomic(file.OriginalPath, bytes);
                written++;
            }

            var report = new RestoreReport
            {
                BackupId = backupId,
                PreRestoreBackupId = preId,
                FilesWritten = written,
                FilesUnchanged = unchanged
            };
            _log.Write(
                LogSeverity.Information,
                "Backup",
                "Restored " + backupId + ". Wrote " + written.ToString(CultureInfo.InvariantCulture)
                + " file(s). Pre-restore backup: " + (preId ?? "none") + ".");
            return OperationResult<RestoreReport>.Success(report);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<RestoreReport>.Failure("The restore was cancelled.");
        }
        catch (Exception ex)
        {
            _log.Write(LogSeverity.Error, "Backup", "Restore failed for " + backupId + ". " + Describe(ex));
            return Fail<RestoreReport>(ex);
        }
    }

    public Task<OperationResult> DeleteAsync(string backupId, bool confirmed, CancellationToken cancellationToken = default)
    {
        if (!confirmed)
        {
            return Task.FromResult(OperationResult.Failure("Deletion was not confirmed."));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = BackupPathRules.ResolveBackupDirectory(_backupsRoot, backupId);
            if (directory is null)
            {
                return Task.FromResult(OperationResult.Failure("The backup id is not valid."));
            }

            if (BackupPathRules.IsReparse(directory))
            {
                return Task.FromResult(OperationResult.Failure("The backup folder is a link and was not deleted."));
            }

            if (!Directory.Exists(directory))
            {
                return Task.FromResult(OperationResult.Failure("That backup was not found."));
            }

            Directory.Delete(directory, recursive: true);
            _log.Write(LogSeverity.Information, "Backup", "Deleted backup " + backupId + ".");
            return Task.FromResult(OperationResult.Success());
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(OperationResult.Failure("The delete was cancelled."));
        }
        catch (Exception ex)
        {
            _log.Write(LogSeverity.Error, "Backup", "Delete failed for " + backupId + ". " + Describe(ex));
            return Task.FromResult(Fail(ex));
        }
    }

    private async Task<OperationResult<BackupDetails>> CreateCoreAsync(string reason, CancellationToken cancellationToken)
    {
        var directory = (string?)null;
        try
        {
            var source = await _source.CaptureAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(source.Error))
            {
                return OperationResult<BackupDetails>.Failure(source.Error!);
            }

            var files = new List<BackupSourceFile>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in source.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = file.Path;
                if (!BackupPathRules.IsAllowedTarget(path, source.InstallRoots, source.UserDirectories)
                    || BackupPathRules.IsReparse(path)
                    || !File.Exists(path)
                    || !seen.Add(Path.GetFullPath(path)))
                {
                    continue;
                }

                var parent = Path.GetDirectoryName(Path.GetFullPath(path));
                if (parent is null || BackupPathRules.IsReparse(parent))
                {
                    continue;
                }

                files.Add(file);
            }

            if (files.Count == 0)
            {
                return OperationResult<BackupDetails>.Failure("No discovered GameLoop configuration files were found to back up.");
            }

            var created = _clock.UtcNow;
            var id = BackupPathRules.NewId(created);
            directory = BackupPathRules.ResolveBackupDirectory(_backupsRoot, id);
            if (directory is null)
            {
                return OperationResult<BackupDetails>.Failure("The backup folder could not be created.");
            }

            Directory.CreateDirectory(directory);
            var entries = new List<BackupFileEntry>();
            var index = 1;
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var full = Path.GetFullPath(file.Path);
                var bytes = ReadLimited(full);
                var hash = BackupPathRules.Sha256(bytes);
                var storedName = index.ToString("00", CultureInfo.InvariantCulture) + "-" + SafeFileName(full);
                index++;
                var storedPath = BackupPathRules.ResolveStoredFile(directory, storedName);
                if (storedPath is null)
                {
                    throw new IOException("The backup file name was not safe.");
                }

                File.WriteAllBytes(storedPath, bytes);
                var copied = ReadLimited(storedPath);
                if (!string.Equals(BackupPathRules.Sha256(copied), hash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException("The backup copy did not match the source hash.");
                }

                DateTimeOffset? written = null;
                try
                {
                    written = new DateTimeOffset(DateTime.SpecifyKind(File.GetLastWriteTimeUtc(full), DateTimeKind.Utc));
                }
                catch (Exception)
                {
                    written = null;
                }

                entries.Add(new BackupFileEntry
                {
                    OriginalPath = full,
                    StoredName = storedName,
                    Sha256 = hash,
                    SizeBytes = bytes.Length,
                    LastWriteTimeUtc = written
                });
            }

            var manifest = new BackupManifest
            {
                Id = id,
                CreatedAtUtc = created,
                CreatedAtLocal = created.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture),
                GameLoopVersion = source.GameLoopVersion,
                Reason = reason,
                Settings = source.Settings,
                Files = entries,
                RegistryValues = source.RegistryValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase),
                UnmappedRegistryKeys = source.UnmappedRegistryKeys.ToList()
            };
            var manifestPath = Path.Combine(directory, BackupPathRules.ManifestFileName);
            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken).ConfigureAwait(false);
            _log.Write(
                LogSeverity.Information,
                "Backup",
                "Created backup " + id + " with " + entries.Count.ToString(CultureInfo.InvariantCulture) + " file(s). Reason: " + reason + ".");
            directory = null;
            return OperationResult<BackupDetails>.Success(new BackupDetails { Id = id, Manifest = manifest });
        }
        catch (OperationCanceledException)
        {
            return OperationResult<BackupDetails>.Failure("The backup was cancelled.");
        }
        catch (Exception ex)
        {
            _log.Write(LogSeverity.Error, "Backup", "Create failed. " + Describe(ex));
            return Fail<BackupDetails>(ex);
        }
        finally
        {
            if (directory is not null && Directory.Exists(directory))
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                }
                catch (Exception)
                {
                    // The failed backup folder is left only when delete itself fails.
                }
            }
        }
    }

    private RestorePreview Plan(BackupManifest manifest, BackupSourceSnapshot source)
    {
        var directory = BackupPathRules.ResolveBackupDirectory(_backupsRoot, manifest.Id);
        var plans = new List<RestoreFilePlan>();
        foreach (var file in manifest.Files)
        {
            plans.Add(PlanFile(directory, file, source));
        }

        var blocked = plans.Any(plan => plan.Disposition == RestoreDisposition.Skip) || plans.Count == 0;
        string? warning = null;
        if (source.GameLoopRunning)
        {
            warning = "GameLoop is running. Close it before restoring. This action does not stop the process.";
        }
        else if (blocked)
        {
            warning = "Restore was refused because one or more files are not safe to replace.";
        }

        return new RestorePreview
        {
            BackupId = manifest.Id,
            Files = plans,
            GameLoopRunning = source.GameLoopRunning,
            CanRestore = !source.GameLoopRunning && !blocked,
            Warning = warning
        };
    }

    private static RestoreFilePlan PlanFile(string? backupDirectory, BackupFileEntry file, BackupSourceSnapshot source)
    {
        var stored = backupDirectory is null ? null : BackupPathRules.ResolveStoredFile(backupDirectory, file.StoredName);
        if (stored is null)
        {
            return Skip(file, "The stored name is not inside the backup folder.");
        }

        if (!File.Exists(stored) || BackupPathRules.IsReparse(stored))
        {
            return Skip(file, "The backup copy is missing or is a link.");
        }

        string backupHash;
        try
        {
            backupHash = BackupPathRules.Sha256(ReadLimited(stored));
        }
        catch (Exception ex)
        {
            return Skip(file, Describe(ex));
        }

        if (!string.Equals(backupHash, file.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return new RestoreFilePlan
            {
                StoredName = file.StoredName,
                OriginalPath = file.OriginalPath,
                BackupSha256 = backupHash,
                Disposition = RestoreDisposition.Skip,
                Detail = "The backup copy does not match its manifest hash."
            };
        }

        if (!BackupPathRules.IsAllowedTarget(file.OriginalPath, source.InstallRoots, source.UserDirectories))
        {
            return Skip(file, "The path is outside the verified install and known user config locations.", backupHash);
        }

        var full = Path.GetFullPath(file.OriginalPath);
        var parent = Path.GetDirectoryName(full);
        if (parent is null || !Directory.Exists(parent))
        {
            return Skip(file, "The target folder does not exist.", backupHash);
        }

        if (BackupPathRules.IsReparse(parent) || BackupPathRules.IsReparse(full))
        {
            return Skip(file, "The target is a link and will not be followed.", backupHash);
        }

        if (!File.Exists(full))
        {
            return new RestoreFilePlan
            {
                StoredName = file.StoredName,
                OriginalPath = full,
                BackupSha256 = backupHash,
                Disposition = RestoreDisposition.Replace,
                Detail = "The live file is missing. Restore would create it."
            };
        }

        string current;
        try
        {
            current = BackupPathRules.Sha256(ReadLimited(full));
        }
        catch (Exception ex)
        {
            return Skip(file, Describe(ex), backupHash);
        }

        var same = string.Equals(current, backupHash, StringComparison.OrdinalIgnoreCase);
        return new RestoreFilePlan
        {
            StoredName = file.StoredName,
            OriginalPath = full,
            CurrentSha256 = current,
            BackupSha256 = backupHash,
            Disposition = same ? RestoreDisposition.Unchanged : RestoreDisposition.Replace,
            Detail = same ? "The live file already matches the backup." : "The live file differs from the backup."
        };
    }

    private static RestoreFilePlan Skip(BackupFileEntry file, string detail, string? backupHash = null) => new()
    {
        StoredName = file.StoredName,
        OriginalPath = string.IsNullOrWhiteSpace(file.OriginalPath) ? file.StoredName : file.OriginalPath,
        BackupSha256 = backupHash ?? file.Sha256,
        Disposition = RestoreDisposition.Skip,
        Detail = detail
    };

    private IReadOnlyList<BackupRecord> ReadRecords()
    {
        if (!Directory.Exists(_backupsRoot) || BackupPathRules.IsReparse(_backupsRoot))
        {
            return [];
        }

        var records = new List<BackupRecord>();
        foreach (var directory in Directory.EnumerateDirectories(_backupsRoot))
        {
            if (BackupPathRules.IsReparse(directory))
            {
                continue;
            }

            var id = Path.GetFileName(directory);
            if (!BackupPathRules.IsSafeId(id))
            {
                continue;
            }

            var loaded = Load(id);
            if (loaded is null)
            {
                continue;
            }

            if (loaded.Damaged || loaded.Manifest is null)
            {
                records.Add(new BackupRecord
                {
                    Id = id,
                    Label = "Damaged backup",
                    CreatedAt = Directory.GetLastWriteTimeUtc(directory),
                    SizeBytes = FolderSize(directory),
                    Damaged = true,
                    Problem = loaded.Problem
                });
                continue;
            }

            records.Add(new BackupRecord
            {
                Id = loaded.Manifest.Id,
                Label = loaded.Manifest.Reason,
                CreatedAt = loaded.Manifest.CreatedAtUtc,
                SizeBytes = loaded.Manifest.Files.Sum(file => file.SizeBytes),
                Damaged = false
            });
        }

        return records.OrderByDescending(record => record.CreatedAt).ToArray();
    }

    private BackupDetails? Load(string backupId)
    {
        var directory = BackupPathRules.ResolveBackupDirectory(_backupsRoot, backupId);
        if (directory is null || !Directory.Exists(directory) || BackupPathRules.IsReparse(directory))
        {
            return directory is null ? null : new BackupDetails
            {
                Id = backupId,
                Damaged = true,
                Problem = "The backup folder is a link and was not opened."
            };
        }

        var manifestPath = Path.Combine(directory, BackupPathRules.ManifestFileName);
        if (!File.Exists(manifestPath) || BackupPathRules.IsReparse(manifestPath))
        {
            return new BackupDetails { Id = backupId, Damaged = true, Problem = "The manifest is missing." };
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(manifestPath), JsonOptions);
            var problem = Problem(manifest, backupId);
            if (problem is not null || manifest is null)
            {
                return new BackupDetails { Id = backupId, Damaged = true, Problem = problem ?? "The manifest could not be read." };
            }

            return new BackupDetails { Id = backupId, Manifest = manifest };
        }
        catch (JsonException)
        {
            return new BackupDetails { Id = backupId, Damaged = true, Problem = "The manifest could not be read." };
        }
    }

    private static string? Problem(BackupManifest? manifest, string directoryId)
    {
        if (manifest is null)
        {
            return "The manifest could not be read.";
        }

        if (!string.Equals(manifest.Id, directoryId, StringComparison.Ordinal))
        {
            return "The manifest id does not match its folder.";
        }

        if (string.IsNullOrWhiteSpace(manifest.Reason) || manifest.CreatedAtUtc == default || manifest.Files is null || manifest.Settings is null)
        {
            return "The manifest is incomplete.";
        }

        foreach (var file in manifest.Files)
        {
            if (string.IsNullOrWhiteSpace(file.OriginalPath) || string.IsNullOrWhiteSpace(file.StoredName) || !BackupPathRules.IsSha256(file.Sha256))
            {
                return "The manifest has an invalid file entry.";
            }
        }

        return null;
    }

    private static string SafeFileName(string path)
    {
        var name = Path.GetFileName(path);
        var safe = new string(name.Where(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_').ToArray());
        if (string.IsNullOrWhiteSpace(safe) || safe is "." or "..")
        {
            safe = "file";
        }

        return safe.Length > 40 ? safe[^40..] : safe;
    }

    private static byte[] ReadLimited(string path)
    {
        var info = new FileInfo(path);
        if (info.Length > BackupPathRules.MaxFileBytes)
        {
            throw new IOException("The file is larger than 1 MB and was not copied.");
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static void WriteAtomic(string target, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(target) ?? throw new IOException("The target folder is missing.");
        var temp = Path.Combine(directory, ".glopt-" + Guid.NewGuid().ToString("N") + ".tmp");
        File.WriteAllBytes(temp, bytes);
        try
        {
            if (File.Exists(target))
            {
                if (BackupPathRules.IsReparse(target))
                {
                    throw new IOException("The target is a link and was not replaced.");
                }

                try
                {
                    File.Replace(temp, target, destinationBackupFileName: null, ignoreMetadataErrors: true);
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                    File.Move(temp, target, overwrite: true);
                    return;
                }
            }

            File.Move(temp, target);
        }
        finally
        {
            if (File.Exists(temp))
            {
                try
                {
                    File.Delete(temp);
                }
                catch (Exception)
                {
                    // The temp file is best-effort.
                }
            }
        }
    }

    private static long FolderSize(string directory)
    {
        long total = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                if (!BackupPathRules.IsReparse(file))
                {
                    total += new FileInfo(file).Length;
                }
            }
        }
        catch (Exception)
        {
            return total;
        }

        return total;
    }

    private static OperationResult<T> Fail<T>(Exception ex) =>
        OperationResult<T>.Failure(Describe(ex));

    private static OperationResult Fail(Exception ex) => OperationResult.Failure(Describe(ex));

    private static string Describe(Exception ex)
    {
        if (ex is UnauthorizedAccessException)
        {
            return "Access was denied. Run GL Optimizer as administrator for this operation only. The rest of the app does not need to be elevated.";
        }

        var message = ex.Message.Trim();
        return message.Length > 240 ? message[..240] : message;
    }
}

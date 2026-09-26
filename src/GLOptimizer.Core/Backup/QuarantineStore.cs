using System.Security.Cryptography;
using GLOptimizer.Core.Detection;

namespace GLOptimizer.Core.Backup;

public interface IFileTransfer
{
    void Copy(string source, string destination);

    void Move(string source, string destination);
}

public sealed class FileTransfer : IFileTransfer
{
    public void Copy(string source, string destination) => File.Copy(source, destination, overwrite: false);

    public void Move(string source, string destination) => File.Move(source, destination);
}

public sealed class QuarantineMoveRequest
{
    public required string BackupDirectory { get; init; }

    public required string InstallRoot { get; init; }

    public required IReadOnlyList<string> SourceFiles { get; init; }

    public bool SameVolume { get; init; } = true;

    public IFileTransfer? Transfer { get; init; }

    public Action<int, string>? BeforeFile { get; init; }
}

public sealed class QuarantineMoveResult
{
    public bool Succeeded { get; init; }

    public bool RolledBack { get; init; }

    public string? Error { get; init; }

    public IReadOnlyList<StoredRepairFile> Moved { get; init; } = [];
}

public static class QuarantineStore
{
    public const string FolderName = "quarantine";

    public static QuarantineMoveResult Move(QuarantineMoveRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var transfer = request.Transfer ?? new FileTransfer();
        var root = InstallPathRules.TryNormalize(request.InstallRoot);
        var backup = NormalizeDirectory(request.BackupDirectory);
        if (root is null || backup is null)
        {
            return Fail("The install or backup path is not safe.");
        }

        var planned = new List<(string Source, string Destination, string Relative)>();
        foreach (var source in request.SourceFiles)
        {
            var full = InstallPathRules.TryNormalize(source);
            var relative = full is null ? null : RelativeUnder(root, full);
            var destination = relative is null ? null : ResolveNested(Path.Combine(backup, FolderName), relative);
            if (full is null || relative is null || destination is null || !File.Exists(full) || IsReparse(full))
            {
                return Fail("A cache path failed re-validation and nothing was moved.");
            }

            planned.Add((full, destination, relative));
        }

        var moved = new List<StoredRepairFile>();
        var placed = new List<(string Quarantine, string Original, string Hash)>();
        try
        {
            for (var index = 0; index < planned.Count; index++)
            {
                if (placed.Count == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var item = planned[index];
                request.BeforeFile?.Invoke(index, item.Source);
                if (!File.Exists(item.Source) || IsReparse(item.Source) || !InstallPathRules.IsUnderRoot(item.Source, root))
                {
                    throw new IOException("A cache path changed before it was moved.");
                }

                var parent = Path.GetDirectoryName(item.Destination);
                if (parent is null || IsReparse(parent))
                {
                    throw new IOException("The quarantine folder is not safe.");
                }

                Directory.CreateDirectory(parent);
                var hash = HashFile(item.Source);
                var size = new FileInfo(item.Source).Length;
                if (request.SameVolume)
                {
                    transfer.Move(item.Source, item.Destination);
                }
                else
                {
                    transfer.Copy(item.Source, item.Destination);
                    if (!string.Equals(HashFile(item.Destination), hash, StringComparison.OrdinalIgnoreCase))
                    {
                        TryDelete(item.Destination);
                        throw new IOException("The quarantine copy did not match the source hash.");
                    }

                    File.Delete(item.Source);
                }

                if (!File.Exists(item.Destination) || !string.Equals(HashFile(item.Destination), hash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException("The quarantined file does not match its hash.");
                }

                placed.Add((item.Destination, item.Source, hash));
                moved.Add(new StoredRepairFile
                {
                    OriginalPath = item.Source,
                    StoredRelativePath = item.Relative.Replace('\\', '/'),
                    Sha256 = hash,
                    SizeBytes = size
                });
            }
        }
        catch (OperationCanceledException)
        {
            var rolled = Rollback(placed, request.SameVolume, transfer);
            return new QuarantineMoveResult
            {
                Succeeded = false,
                RolledBack = rolled,
                Error = rolled
                    ? "The repair was cancelled and moved files were put back."
                    : "The repair was cancelled and a moved file could not be put back."
            };
        }
        catch (Exception ex)
        {
            var rolled = Rollback(placed, request.SameVolume, transfer);
            return new QuarantineMoveResult
            {
                Succeeded = false,
                RolledBack = rolled,
                Error = (rolled
                    ? "The repair stopped and moved files were put back. "
                    : "The repair stopped and a moved file could not be put back. ")
                    + Trim(ex.Message)
            };
        }

        return new QuarantineMoveResult { Succeeded = true, Moved = moved };
    }

    public static string? Restore(string backupDirectory, StoredRepairFile entry, IReadOnlyList<string> installRoots, IReadOnlyList<string> userDirectories) =>
        RestoreFrom(backupDirectory, FolderName, entry, installRoots, userDirectories);

    public static string? RestoreMetadata(string backupDirectory, StoredRepairFile entry, IReadOnlyList<string> installRoots, IReadOnlyList<string> userDirectories) =>
        RestoreFrom(backupDirectory, MetadataFolderName, entry, installRoots, userDirectories);

    private static string? RestoreFrom(string backupDirectory, string folder, StoredRepairFile entry, IReadOnlyList<string> installRoots, IReadOnlyList<string> userDirectories)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!BackupPathRules.IsSha256(entry.Sha256) || string.IsNullOrWhiteSpace(entry.StoredRelativePath))
        {
            return "The quarantine entry is incomplete.";
        }

        var stored = ResolveNested(Path.Combine(backupDirectory, folder), entry.StoredRelativePath);
        var original = InstallPathRules.TryNormalize(entry.OriginalPath);
        if (stored is null || original is null || !File.Exists(stored) || IsReparse(stored))
        {
            return "The quarantined file is missing or is a link.";
        }

        if (!BackupPathRules.IsAllowedTarget(original, installRoots, userDirectories))
        {
            return "The original path is outside the verified install.";
        }

        if (!string.Equals(HashFile(stored), entry.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return "The quarantined file does not match its manifest hash.";
        }

        var parent = Path.GetDirectoryName(original);
        if (parent is null || IsReparse(parent) || IsReparse(original))
        {
            return "The original path is a link and was not restored.";
        }

        Directory.CreateDirectory(parent);
        var temp = Path.Combine(parent, ".glopt-" + Guid.NewGuid().ToString("N") + ".tmp");
        File.Copy(stored, temp, overwrite: false);
        if (!string.Equals(HashFile(temp), entry.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(temp);
            return "The restored copy did not match its manifest hash.";
        }

        if (File.Exists(original))
        {
            if (IsReparse(original))
            {
                TryDelete(temp);
                return "The original path is a link and was not restored.";
            }

            File.Delete(original);
        }

        File.Move(temp, original);
        return null;
    }

    public const string MetadataFolderName = "metadata";

    public static StoredRepairFile? CopyMetadata(string backupDirectory, string installRoot, string sourcePath)
    {
        var root = InstallPathRules.TryNormalize(installRoot);
        var source = InstallPathRules.TryNormalize(sourcePath);
        var backup = NormalizeDirectory(backupDirectory);
        var relative = root is null || source is null ? null : RelativeUnder(root, source);
        var destination = relative is null || backup is null ? null : ResolveNested(Path.Combine(backup, MetadataFolderName), relative);
        if (source is null || destination is null || !File.Exists(source) || IsReparse(source))
        {
            return null;
        }

        var parent = Path.GetDirectoryName(destination);
        if (parent is null)
        {
            return null;
        }

        Directory.CreateDirectory(parent);
        File.Copy(source, destination, overwrite: false);
        var hash = HashFile(source);
        if (!string.Equals(HashFile(destination), hash, StringComparison.OrdinalIgnoreCase))
        {
            TryDelete(destination);
            return null;
        }

        return new StoredRepairFile
        {
            OriginalPath = source,
            StoredRelativePath = relative!.Replace('\\', '/'),
            Sha256 = hash,
            SizeBytes = new FileInfo(source).Length
        };
    }

    public static string HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool Rollback(IReadOnlyList<(string Quarantine, string Original, string Hash)> placed, bool sameVolume, IFileTransfer transfer)
    {
        var ok = true;
        for (var index = placed.Count - 1; index >= 0; index--)
        {
            var entry = placed[index];
            try
            {
                if (!File.Exists(entry.Quarantine))
                {
                    ok = false;
                    continue;
                }

                var parent = Path.GetDirectoryName(entry.Original);
                if (parent is null)
                {
                    ok = false;
                    continue;
                }

                Directory.CreateDirectory(parent);
                if (sameVolume)
                {
                    transfer.Move(entry.Quarantine, entry.Original);
                }
                else
                {
                    transfer.Copy(entry.Quarantine, entry.Original);
                    if (!string.Equals(HashFile(entry.Original), entry.Hash, StringComparison.OrdinalIgnoreCase))
                    {
                        TryDelete(entry.Original);
                        ok = false;
                        continue;
                    }

                    File.Delete(entry.Quarantine);
                }
            }
            catch (Exception)
            {
                ok = false;
            }
        }

        return ok;
    }

    public static string? RelativeUnder(string root, string full)
    {
        string relative;
        try
        {
            relative = Path.GetRelativePath(root, full);
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (relative is "." or "" || relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            return null;
        }

        return relative;
    }

    public static string? ResolveNested(string root, string? relative)
    {
        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(relative))
        {
            return null;
        }

        var normalized = relative.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (normalized.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(normalized))
        {
            return null;
        }

        try
        {
            var fullRoot = Path.GetFullPath(root);
            var full = Path.GetFullPath(Path.Combine(fullRoot, normalized));
            return InstallPathRules.IsUnderRoot(full, fullRoot) ? full : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static string? NormalizeDirectory(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
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

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path) && !IsReparse(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // The caller reports the failed transfer.
        }
    }

    private static QuarantineMoveResult Fail(string error) => new() { Error = error };

    private static string Trim(string message)
    {
        var text = message.Trim();
        return text.Length > 200 ? text[..200] : text;
    }
}

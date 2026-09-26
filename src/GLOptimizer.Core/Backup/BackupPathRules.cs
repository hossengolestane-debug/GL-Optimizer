using System.Globalization;
using GLOptimizer.Core.Detection;

namespace GLOptimizer.Core.Backup;

public static class BackupPathRules
{
    public const int MaxReasonLength = 200;
    public const long MaxFileBytes = 1_048_576;
    public const string ManifestFileName = "manifest.json";

    public static bool IsSafeId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64)
        {
            return false;
        }

        if (!char.IsAsciiLetterOrDigit(id[0]))
        {
            return false;
        }

        foreach (var character in id)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-')
            {
                return false;
            }
        }

        return true;
    }

    public static string? NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var trimmed = reason.Trim();
        if (trimmed.Length > MaxReasonLength)
        {
            return null;
        }

        foreach (var character in trimmed)
        {
            if (char.IsControl(character))
            {
                return null;
            }
        }

        return trimmed;
    }

    public static bool IsSha256(string? value) =>
        value is not null
        && value.Length == 64
        && value.All(Uri.IsHexDigit);

    public static string? ResolveStoredFile(string backupDirectory, string? storedName)
    {
        if (string.IsNullOrWhiteSpace(backupDirectory) || string.IsNullOrWhiteSpace(storedName))
        {
            return null;
        }

        if (!string.Equals(storedName, Path.GetFileName(storedName), StringComparison.Ordinal))
        {
            return null;
        }

        if (storedName is "." or ".." || storedName.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        string full;
        string root;
        try
        {
            root = Path.GetFullPath(backupDirectory);
            full = Path.GetFullPath(Path.Combine(root, storedName));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        return InstallPathRules.IsUnderRoot(full, root) && !PathsEqual(full, root) ? full : null;
    }

    public static string? ResolveBackupDirectory(string backupsRoot, string? id)
    {
        if (!IsSafeId(id) || string.IsNullOrWhiteSpace(backupsRoot))
        {
            return null;
        }

        var safeId = id!;
        try
        {
            var root = Path.GetFullPath(backupsRoot);
            var full = Path.GetFullPath(Path.Combine(root, safeId));
            var parent = Path.GetDirectoryName(full);
            if (parent is null || !PathsEqual(parent, root))
            {
                return null;
            }

            return full;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    public static bool IsAllowedTarget(string? path, IReadOnlyList<string> installRoots, IReadOnlyList<string> userDirectories)
    {
        var full = InstallPathRules.TryNormalize(path);
        if (full is null)
        {
            return false;
        }

        if (installRoots is not null)
        {
            foreach (var root in installRoots)
            {
                if (InstallPathRules.IsUnderRoot(full, root) && !PathsEqual(full, root))
                {
                    return true;
                }
            }
        }

        if (userDirectories is not null)
        {
            foreach (var root in userDirectories)
            {
                if (InstallPathRules.IsUnderRoot(full, root) && !PathsEqual(full, root))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool IsReparse(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

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

    public static string Sha256(byte[] bytes) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));

    public static string NewId(DateTimeOffset utc) =>
        utc.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture)
        + "-"
        + Guid.NewGuid().ToString("N")[..8];

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            left.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            right.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
}

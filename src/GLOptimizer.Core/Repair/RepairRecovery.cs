using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Detection;

namespace GLOptimizer.Core.Repair;

public static class RepairRecovery
{
    public static bool NeedsRollback(RepairCheckpoint? checkpoint) =>
        checkpoint is { InProgress: true } && !string.IsNullOrWhiteSpace(checkpoint.BackupId);

    /// <summary>
    /// Maps a path that is relative to the quarantine folder onto one saved install root.
    /// Parent segments, rooted paths, and reparse points are rejected.
    /// </summary>
    public static string? OriginalPath(IReadOnlyList<string> installRoots, string relativeUnderQuarantine)
    {
        if (installRoots is null || installRoots.Count == 0 || string.IsNullOrWhiteSpace(relativeUnderQuarantine))
        {
            return null;
        }

        var relative = relativeUnderQuarantine.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(relative))
        {
            return null;
        }

        var segments = relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            return null;
        }

        string? match = null;
        var joined = string.Join(Path.DirectorySeparatorChar, segments);
        foreach (var root in installRoots)
        {
            var normalizedRoot = InstallPathRules.TryNormalize(root);
            if (normalizedRoot is null)
            {
                continue;
            }

            string combined;
            try
            {
                combined = Path.GetFullPath(Path.Combine(normalizedRoot, joined));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }

            if (!InstallPathRules.IsUnderRoot(combined, normalizedRoot) || IsReparse(combined))
            {
                continue;
            }

            var parent = Path.GetDirectoryName(combined);
            if (parent is null || IsReparse(parent))
            {
                continue;
            }

            if (match is not null)
            {
                return null;
            }

            match = combined;
        }

        return match;
    }

    private static bool IsReparse(string? path)
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
}

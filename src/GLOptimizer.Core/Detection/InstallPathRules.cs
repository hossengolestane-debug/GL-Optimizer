namespace GLOptimizer.Core.Detection;

/// <summary>
/// Normalizes install and executable paths. Relative paths, URIs, and filesystem roots are rejected.
/// </summary>
public static class InstallPathRules
{
    /// <summary>
    /// Takes the first quoted path, or the path through ".exe", then normalizes it.
    /// Trailing uninstall arguments are not part of the path.
    /// </summary>
    public static string? TryNormalizeCommand(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        var trimmed = command.Trim();
        string? extracted = null;
        if (trimmed.Length > 1 && trimmed[0] == '"')
        {
            var end = trimmed.IndexOf('"', 1);
            if (end > 1)
            {
                extracted = trimmed[1..end];
            }
        }

        if (extracted is null)
        {
            var exe = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (exe >= 0)
            {
                var after = exe + 4;
                if (after == trimmed.Length || trimmed[after] is ' ' or ',')
                {
                    extracted = trimmed[..after];
                }
            }
        }

        return TryNormalize(extracted ?? trimmed);
    }

    public static string? TryNormalize(string? path)
    {
        var prepared = Prepare(path);
        if (prepared is null)
        {
            return null;
        }

        try
        {
            var full = Path.GetFullPath(prepared);
            return IsFileSystemRoot(full) ? null : full;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    public static bool IsUnderRoot(string? candidate, string? root)
    {
        var fullRoot = TryNormalize(root);
        if (fullRoot is null)
        {
            return false;
        }

        var prepared = Prepare(candidate);
        if (prepared is null)
        {
            return false;
        }

        string fullCandidate;
        try
        {
            fullCandidate = Path.GetFullPath(prepared);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        var prefix = WithSeparator(fullRoot);
        if (fullCandidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(TrimSeparators(fullCandidate), TrimSeparators(fullRoot), StringComparison.OrdinalIgnoreCase);
    }

    private static string? Prepare(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var trimmed = path.Trim().Trim('"');
        if (trimmed.Contains("://", StringComparison.Ordinal))
        {
            return null;
        }

        var lastSeparator = Math.Max(trimmed.LastIndexOf('\\'), trimmed.LastIndexOf('/'));
        var comma = trimmed.LastIndexOf(',');
        if (comma > lastSeparator && comma > 0)
        {
            var suffix = trimmed[(comma + 1)..].Trim();
            if (int.TryParse(suffix, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _))
            {
                trimmed = trimmed[..comma].Trim().Trim('"');
            }
        }

        if (trimmed.Length == 0 || !Path.IsPathRooted(trimmed))
        {
            return null;
        }

        return trimmed;
    }

    private static bool IsFileSystemRoot(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            return true;
        }

        return string.Equals(TrimSeparators(fullPath), TrimSeparators(root), StringComparison.OrdinalIgnoreCase);
    }

    private static string WithSeparator(string path)
    {
        var trimmed = TrimSeparators(path);
        return trimmed + Path.DirectorySeparatorChar;
    }

    private static string TrimSeparators(string path) =>
        path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}

namespace GLOptimizer.Core.Detection;

/// <summary>
/// A GameLoop path override is accepted only when the folder contains a known launcher file.
/// </summary>
public static class InstallOverrideRules
{
    public static string? Validate(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var root = InstallPathRules.TryNormalize(path);
        if (root is null || !Directory.Exists(root))
        {
            return "The GameLoop path is not a folder.";
        }

        if (IsReparse(root))
        {
            return "The GameLoop path is a link and was not accepted.";
        }

        foreach (var segments in GameLoopLayout.LauncherSegments)
        {
            var file = GameLoopLayout.Combine(root, segments);
            if (File.Exists(file) && InstallPathRules.IsUnderRoot(file, root) && !IsReparse(file))
            {
                return null;
            }
        }

        return "The folder does not contain a verified GameLoop launcher.";
    }

    public static string? Candidate(string? path) => Validate(path) is null && !string.IsNullOrWhiteSpace(path)
        ? InstallPathRules.TryNormalize(path)
        : null;

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
}

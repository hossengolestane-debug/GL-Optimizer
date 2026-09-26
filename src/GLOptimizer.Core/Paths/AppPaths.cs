namespace GLOptimizer.Core.Paths;

/// <summary>
/// Locations under %LocalAppData%\GLOptimizer. Paths are computed only; nothing is created here.
/// </summary>
public static class AppPaths
{
    public const string FolderName = "GLOptimizer";
    public const string LogsFolderName = "Logs";
    public const string BackupsFolderName = "Backups";
    public const string SettingsFileName = "settings.json";
    public const string ActiveLogFileName = "gloptimizer.log";

    public static string GetRoot(string localAppData)
    {
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new ArgumentException("Local application data path is required.", nameof(localAppData));
        }

        return Path.Combine(localAppData, FolderName);
    }

    public static string GetBackupsDirectory(string root)
    {
        RequireRoot(root);
        return Path.Combine(root, BackupsFolderName);
    }

    public static string GetLogsDirectory(string root)
    {
        RequireRoot(root);
        return Path.Combine(root, LogsFolderName);
    }

    public static string GetSettingsFile(string root)
    {
        RequireRoot(root);
        return Path.Combine(root, SettingsFileName);
    }

    public static string GetActiveLogFile(string logsDirectory)
    {
        if (string.IsNullOrWhiteSpace(logsDirectory))
        {
            throw new ArgumentException("Logs directory is required.", nameof(logsDirectory));
        }

        return Path.Combine(logsDirectory, ActiveLogFileName);
    }

    public static string GetDefaultRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return GetRoot(localAppData);
    }

    private static void RequireRoot(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new ArgumentException("Application root path is required.", nameof(root));
        }
    }
}

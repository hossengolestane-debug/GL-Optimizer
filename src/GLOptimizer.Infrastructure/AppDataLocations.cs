using GLOptimizer.Core.Paths;

namespace GLOptimizer.Infrastructure;

public sealed class AppDataLocations
{
    public AppDataLocations(string localAppData)
    {
        Root = AppPaths.GetRoot(localAppData);
        LogsDirectory = AppPaths.GetLogsDirectory(Root);
        SettingsFile = AppPaths.GetSettingsFile(Root);
        ActiveLogFile = AppPaths.GetActiveLogFile(LogsDirectory);
    }

    public string Root { get; }

    public string LogsDirectory { get; }

    public string SettingsFile { get; }

    public string ActiveLogFile { get; }
}

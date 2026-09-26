using GLOptimizer.Core.Detection;

namespace GLOptimizer.Core.Repair;

public sealed class ControlledProcess
{
    public int ProcessId { get; init; }

    public string ProcessName { get; init; } = string.Empty;

    public string? ExecutablePath { get; init; }
}

public static class ProcessSelection
{
    public const string SessionWarning =
        "A COD Mobile or PUBG Mobile session appears active. Stopping GameLoop can close that session.";

    public static bool IsGameLoopFamily(string? name) => GameLoopNames.IsProcess(name);

    public static IReadOnlyList<ControlledProcess> SelectStopTargets(
        IReadOnlyList<ControlledProcess> processes,
        IReadOnlyList<string> installRoots)
    {
        ArgumentNullException.ThrowIfNull(processes);
        var selected = new List<ControlledProcess>();
        foreach (var process in processes)
        {
            if (!IsGameLoopFamily(process.ProcessName) || string.IsNullOrWhiteSpace(process.ExecutablePath))
            {
                continue;
            }

            if (UnderAny(process.ExecutablePath, installRoots))
            {
                selected.Add(process);
            }
        }

        return selected;
    }

    public static bool GameSessionAppearsActive(
        IReadOnlyList<ControlledProcess> processes,
        IReadOnlyList<string> windowTitles,
        IReadOnlyList<string> installRoots)
    {
        foreach (var process in processes)
        {
            if (string.IsNullOrWhiteSpace(process.ExecutablePath) || !UnderAny(process.ExecutablePath, installRoots))
            {
                continue;
            }

            if (IsSessionProcess(process))
            {
                return true;
            }
        }

        foreach (var title in windowTitles)
        {
            if (title.Contains("Call of Duty", StringComparison.OrdinalIgnoreCase)
                || title.Contains("COD Mobile", StringComparison.OrdinalIgnoreCase)
                || title.Contains("PUBG", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSessionProcess(ControlledProcess process)
    {
        var name = process.ProcessName;
        if (name.Contains("codm", StringComparison.OrdinalIgnoreCase)
            || name.Contains("callofduty", StringComparison.OrdinalIgnoreCase)
            || name.Contains("pubg", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var path = process.ExecutablePath ?? string.Empty;
        foreach (var packageId in MobilePackages.Cod.Concat(MobilePackages.Pubg))
        {
            if (path.Contains(packageId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool UnderAny(string path, IReadOnlyList<string> roots)
    {
        foreach (var root in roots)
        {
            if (InstallPathRules.IsUnderRoot(path, root))
            {
                return true;
            }
        }

        return false;
    }
}

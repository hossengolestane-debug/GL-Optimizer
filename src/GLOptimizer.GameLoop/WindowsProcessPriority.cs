using System.Diagnostics;
using System.Runtime.Versioning;
using GLOptimizer.Core.Launch;

namespace GLOptimizer.GameLoop;

public sealed class WindowsProcessPriority : IProcessPriority
{
    public bool IsRunning(int processId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return RunningOnWindows(processId);
    }

    public string? ReadPriority(int processId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        return ReadOnWindows(processId);
    }

    public bool TrySet(int processId, string priorityName)
    {
        if (!PriorityNames.CanApply(priorityName) && !PriorityNames.CanRestore(priorityName))
        {
            return false;
        }

        if (priorityName.Equals(PriorityNames.Realtime, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return SetOnWindows(processId, priorityName);
    }

    [SupportedOSPlatform("windows")]
    private static bool RunningOnWindows(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (Exception)
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadOnWindows(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.PriorityClass.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool SetOnWindows(int processId, string priorityName)
    {
        if (!Enum.TryParse<ProcessPriorityClass>(priorityName, ignoreCase: true, out var level) || level == ProcessPriorityClass.RealTime)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            process.PriorityClass = level;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

using System.Diagnostics;
using System.Runtime.Versioning;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Repair;

namespace GLOptimizer.GameLoop;

public sealed class WindowsProcessControl : IProcessControl
{
    public IReadOnlyList<ControlledProcess> List()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        return ListOnWindows();
    }

    public bool TryCloseMainWindow(int processId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return CloseOnWindows(processId);
    }

    public bool TryTerminate(int processId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        return TerminateOnWindows(processId);
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<ControlledProcess> ListOnWindows()
    {
        var found = new List<ControlledProcess>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                string? path = null;
                try
                {
                    path = process.MainModule?.FileName;
                }
                catch (Exception)
                {
                    path = null;
                }

                found.Add(new ControlledProcess
                {
                    ProcessId = process.Id,
                    ProcessName = process.ProcessName,
                    ExecutablePath = path
                });
            }
            catch (Exception)
            {
                // A process that disappears is not a stop target.
            }
            finally
            {
                process.Dispose();
            }
        }

        return found;
    }

    [SupportedOSPlatform("windows")]
    private static bool CloseOnWindows(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.CloseMainWindow();
        }
        catch (Exception)
        {
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool TerminateOnWindows(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: false);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

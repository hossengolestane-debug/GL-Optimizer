using System.Diagnostics;
using System.Runtime.Versioning;
using GLOptimizer.Core.Elevation;
using GLOptimizer.Core.Results;

namespace GLOptimizer.GameLoop;

public sealed class WindowsElevationRelaunch : IElevationRelaunch
{
    public OperationResult Relaunch(string operation)
    {
        if (!ElevationPolicy.IsOperation(operation))
        {
            return OperationResult.Failure("That operation is not relaunched on its own.");
        }

        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(Environment.ProcessPath))
        {
            return OperationResult.Failure("Elevation is only available on Windows.");
        }

        try
        {
            StartOnWindows(operation);
            return OperationResult.Success();
        }
        catch (Exception)
        {
            return OperationResult.Failure(ElevationPolicy.Explain(operation));
        }
    }

    [SupportedOSPlatform("windows")]
    private static void StartOnWindows(string operation)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = Environment.ProcessPath!,
            Arguments = ElevationPolicy.Argument(operation)!,
            UseShellExecute = true,
            Verb = "runas"
        });
    }
}

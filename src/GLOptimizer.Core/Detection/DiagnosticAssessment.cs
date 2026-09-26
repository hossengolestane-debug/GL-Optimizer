using GLOptimizer.Core.Models;

namespace GLOptimizer.Core.Detection;

public static class DiagnosticAssessment
{
    public static DiagnosticState Evaluate(HardwareReport? hardware, GameLoopScan? scan, bool hardwareFailed, bool scanFailed)
    {
        var noInstall = scan is null || scan.Installations.Count == 0;
        if (hardwareFailed && scanFailed)
        {
            return DiagnosticState.ActionRequired;
        }

        if (scan?.BrokenRegistration == true && noInstall)
        {
            return DiagnosticState.ActionRequired;
        }

        if (scanFailed || hardwareFailed || noInstall)
        {
            return DiagnosticState.Warning;
        }

        var useful = !string.IsNullOrWhiteSpace(hardware?.CpuName) || hardware?.TotalMemoryBytes is > 0;
        return useful ? DiagnosticState.Good : DiagnosticState.Warning;
    }
}

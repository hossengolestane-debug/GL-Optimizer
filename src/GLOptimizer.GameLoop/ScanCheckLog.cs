using System.Globalization;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Models;

namespace GLOptimizer.GameLoop;

/// <summary>
/// Writes the scan's checked list as one log record per item so a miss can be read from the log.
/// </summary>
public static class ScanCheckLog
{
    public static void Write(ILogStore log, GameLoopScan? scan)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (scan is null)
        {
            return;
        }

        log.Write(
            LogSeverity.Information,
            "Scan",
            "Scan checks: " + scan.Checks.Count.ToString(CultureInfo.InvariantCulture));
        foreach (var check in scan.Checks)
        {
            var line = check.Kind + " " + check.Target + ": " + (check.Found ? "found" : "not found");
            if (!string.IsNullOrWhiteSpace(check.Detail))
            {
                line += " (" + check.Detail + ")";
            }

            log.Write(LogSeverity.Information, "Scan", line);
        }
    }
}

using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Models;

namespace GLOptimizer.Core.Detection;

public static class DetectionText
{
    public static string RunStatus(GameRunStatus status) => status switch
    {
        GameRunStatus.Running => "Running",
        GameRunStatus.Stopped => "Stopped",
        _ => "Unknown"
    };

    public static string Presence(GamePresenceStatus status) => status switch
    {
        GamePresenceStatus.Installed => "Installed",
        GamePresenceStatus.NotFound => "Not found",
        _ => "Unknown"
    };

    public static string State(DiagnosticState state) => state switch
    {
        DiagnosticState.Good => "GOOD",
        DiagnosticState.Warning => "WARNING",
        DiagnosticState.ActionRequired => "ACTION REQUIRED",
        _ => "UNKNOWN"
    };

    public static StatusKind StateKind(DiagnosticState state) => state switch
    {
        DiagnosticState.Good => StatusKind.Ready,
        DiagnosticState.Warning => StatusKind.Attention,
        DiagnosticState.ActionRequired => StatusKind.Attention,
        _ => StatusKind.Neutral
    };

    public static StatusKind PresenceKind(GamePresenceStatus status) => status switch
    {
        GamePresenceStatus.Installed => StatusKind.Ready,
        GamePresenceStatus.NotFound => StatusKind.Attention,
        _ => StatusKind.Unavailable
    };

    public static StatusKind RunKind(GameRunStatus status) => status switch
    {
        GameRunStatus.Running => StatusKind.Ready,
        GameRunStatus.Stopped => StatusKind.Neutral,
        _ => StatusKind.Unavailable
    };

    public readonly record struct ProductStatus(string Badge, StatusKind Kind);

    public static ProductStatus ForGameLoop(GameLoopScan? scan, bool scanFailed)
    {
        if (scanFailed || scan is null)
        {
            return new ProductStatus("Unknown", StatusKind.Unavailable);
        }

        if (scan.Installations.Count == 0)
        {
            return new ProductStatus("Not found", StatusKind.Attention);
        }

        if (scan.Installations.Any(install => install.RunStatus == GameRunStatus.Running))
        {
            return new ProductStatus("Ready", StatusKind.Ready);
        }

        return new ProductStatus("Installed", StatusKind.Ready);
    }

    public static ProductStatus ForMobile(MobileGamePresence? presence, bool scanFailed)
    {
        if (scanFailed || presence is null)
        {
            return new ProductStatus("Unknown", StatusKind.Unavailable);
        }

        return new ProductStatus(Presence(presence.Status), PresenceKind(presence.Status));
    }
}

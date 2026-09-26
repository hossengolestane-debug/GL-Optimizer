using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Launch;
using GLOptimizer.Core.Optimization;
using GLOptimizer.Core.Repair;

namespace GLOptimizer.Core.Recovery;

public enum RecoveryKind
{
    None,
    Repair,
    Optimization,
    Launch
}

public sealed class StartupRecovery
{
    public RecoveryKind Kind { get; init; }

    public string Message { get; init; } = string.Empty;

    public string ActionLabel { get; init; } = string.Empty;
}

public static class StartupRecoveryLogic
{
    public static StartupRecovery Inspect(RepairCheckpoint? repair, OptimizationUndoRecord? optimization, LaunchRecovery launch)
    {
        ArgumentNullException.ThrowIfNull(launch);
        if (RepairRecovery.NeedsRollback(repair))
        {
            return new StartupRecovery
            {
                Kind = RecoveryKind.Repair,
                Message = "An App Market repair was interrupted. Cache may be in quarantine. Rollback puts those files back and does not delete anything else.",
                ActionLabel = "Roll back repair"
            };
        }

        if (optimization is { InProgress: true } && !string.IsNullOrWhiteSpace(optimization.BackupId))
        {
            return new StartupRecovery
            {
                Kind = RecoveryKind.Optimization,
                Message = "An optimization apply was interrupted. The pre-apply backup can be restored. Close GameLoop first if it is running.",
                ActionLabel = "Restore backup"
            };
        }

        if (launch.JournalPresent)
        {
            return new StartupRecovery
            {
                Kind = RecoveryKind.Launch,
                Message = string.IsNullOrWhiteSpace(launch.Message)
                    ? "A previous Launch Optimized session did not finish. Recovery can restore the saved priorities."
                    : launch.Message,
                ActionLabel = "Restore priorities"
            };
        }

        return new StartupRecovery();
    }
}

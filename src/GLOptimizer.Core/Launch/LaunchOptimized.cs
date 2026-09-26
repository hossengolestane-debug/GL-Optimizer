using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Repair;

namespace GLOptimizer.Core.Launch;

public static class PriorityNames
{
    public const string AboveNormal = "AboveNormal";

    public const string Realtime = "Realtime";

    public static bool CanApply(string? name) =>
        name is not null && name.Equals(AboveNormal, StringComparison.OrdinalIgnoreCase);

    public static bool CanRestore(string? name) =>
        name is not null
        && !name.Equals(Realtime, StringComparison.OrdinalIgnoreCase)
        && name is "Normal" or "AboveNormal" or "BelowNormal" or "Idle" or "High";
}

public sealed class PriorityChange
{
    public int ProcessId { get; set; }

    public string ExecutablePath { get; set; } = string.Empty;

    public string PreviousPriority { get; set; } = "Normal";
}

public sealed class LaunchJournal
{
    public DateTimeOffset StartedAtUtc { get; set; }

    public string InstallPath { get; set; } = string.Empty;

    public List<PriorityChange> Changes { get; set; } = [];
}

public interface IProcessPriority
{
    bool IsRunning(int processId);

    string? ReadPriority(int processId);

    bool TrySet(int processId, string priorityName);
}

public interface ILaunchJournalStore
{
    string? LastProblem => null;

    LaunchJournal? Load();

    void Save(LaunchJournal journal);

    void Clear();
}

public sealed class LaunchOptimizedPlan
{
    public bool CanApply { get; init; }

    public string? Error { get; init; }

    public IReadOnlyList<ControlledProcess> Targets { get; init; } = [];
}

public static class LaunchOptimizedRules
{
    public const string ForbiddenMessage = "Only AboveNormal is applied. Realtime is never set.";

    public const string NotImplementedPower = "Power plan changes are not implemented. They stay a recommendation.";

    public const string NotImplementedGraphics = "Graphics preference changes are not implemented. They stay a recommendation.";

    public static LaunchOptimizedPlan Plan(IReadOnlyList<ControlledProcess> processes, IReadOnlyList<string> installRoots, string requestedPriority)
    {
        if (requestedPriority.Equals(PriorityNames.Realtime, StringComparison.OrdinalIgnoreCase))
        {
            return new LaunchOptimizedPlan { Error = ForbiddenMessage };
        }

        if (!PriorityNames.CanApply(requestedPriority))
        {
            return new LaunchOptimizedPlan { Error = ForbiddenMessage };
        }

        var targets = ProcessSelection.SelectStopTargets(processes, installRoots);
        return targets.Count == 0
            ? new LaunchOptimizedPlan { Error = "No verified GameLoop process is running." }
            : new LaunchOptimizedPlan { CanApply = true, Targets = targets };
    }
}

public sealed class LaunchRecovery
{
    public bool JournalPresent { get; init; }

    public string Message { get; init; } = string.Empty;

    public IReadOnlyList<PriorityChange> Changes { get; init; } = [];
}

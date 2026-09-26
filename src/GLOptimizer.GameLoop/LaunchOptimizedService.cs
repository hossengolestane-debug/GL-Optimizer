using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Elevation;
using GLOptimizer.Core.Launch;
using GLOptimizer.Core.Repair;
using GLOptimizer.Core.Results;

namespace GLOptimizer.GameLoop;

public sealed class LaunchOptimizedService : ILaunchOptimized
{
    private readonly IGameLoopDetector _detector;
    private readonly IProcessControl _processes;
    private readonly IProcessPriority _priority;
    private readonly ILaunchJournalStore _journal;
    private readonly IClock _clock;

    public LaunchOptimizedService(
        IGameLoopDetector detector,
        IProcessControl processes,
        IProcessPriority priority,
        ILaunchJournalStore journal,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(priority);
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(clock);
        _detector = detector;
        _processes = processes;
        _priority = priority;
        _journal = journal;
        _clock = clock;
    }

    public LaunchRecovery Inspect()
    {
        var journal = _journal.Load();
        if (journal is null)
        {
            return new LaunchRecovery { Message = "No Launch Optimized session is waiting." };
        }

        return new LaunchRecovery
        {
            JournalPresent = true,
            Changes = journal.Changes,
            Message = "A previous Launch Optimized session did not finish. Recovery can restore the saved priorities."
        };
    }

    public async Task<OperationResult> ApplyAsync(bool confirmed, CancellationToken cancellationToken = default)
    {
        if (!confirmed)
        {
            return OperationResult.Failure("Launch Optimized was not confirmed.");
        }

        var scan = await _detector.DetectAsync(cancellationToken).ConfigureAwait(false);
        if (!scan.Succeeded || scan.Value is null)
        {
            return OperationResult.Failure(scan.Error ?? "GameLoop could not be scanned.");
        }

        var roots = scan.Value.Installations
            .Select(install => install.InstallPath)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToArray();
        var plan = LaunchOptimizedRules.Plan(_processes.List(), roots, PriorityNames.AboveNormal);
        if (!plan.CanApply)
        {
            return OperationResult.Failure(plan.Error ?? LaunchOptimizedRules.ForbiddenMessage);
        }

        var changes = new List<PriorityChange>();
        foreach (var target in plan.Targets)
        {
            var previous = _priority.ReadPriority(target.ProcessId) ?? "Normal";
            if (!PriorityNames.CanRestore(previous))
            {
                previous = "Normal";
            }

            if (!_priority.TrySet(target.ProcessId, PriorityNames.AboveNormal))
            {
                RestoreApplied(changes);
                return OperationResult.Failure(ElevationPolicy.Explain("Launch Optimized"));
            }

            changes.Add(new PriorityChange
            {
                ProcessId = target.ProcessId,
                ExecutablePath = target.ExecutablePath ?? string.Empty,
                PreviousPriority = previous
            });
        }

        _journal.Save(new LaunchJournal
        {
            StartedAtUtc = _clock.UtcNow,
            InstallPath = roots.FirstOrDefault() ?? string.Empty,
            Changes = changes
        });
        return OperationResult.Success();
    }

    public Task<OperationResult> RecoverAsync(bool restore, CancellationToken cancellationToken = default)
    {
        var journal = _journal.Load();
        if (journal is null)
        {
            return Task.FromResult(OperationResult.Failure("No Launch Optimized session is waiting."));
        }

        if (restore)
        {
            foreach (var change in journal.Changes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_priority.IsRunning(change.ProcessId))
                {
                    continue;
                }

                if (!PriorityNames.CanRestore(change.PreviousPriority))
                {
                    continue;
                }

                _priority.TrySet(change.ProcessId, change.PreviousPriority);
            }
        }

        _journal.Clear();
        return Task.FromResult(OperationResult.Success());
    }

    public OperationResult PowerPlan() => OperationResult.NotImplemented("Power plan changes");

    public OperationResult GraphicsPreference() => OperationResult.NotImplemented("Graphics preference changes");

    private void RestoreApplied(IReadOnlyList<PriorityChange> changes)
    {
        foreach (var change in changes)
        {
            if (PriorityNames.CanRestore(change.PreviousPriority))
            {
                _priority.TrySet(change.ProcessId, change.PreviousPriority);
            }
        }
    }
}

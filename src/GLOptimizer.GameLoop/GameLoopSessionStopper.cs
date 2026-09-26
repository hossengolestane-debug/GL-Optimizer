using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Repair;

namespace GLOptimizer.GameLoop;

public sealed class StopResult
{
    public bool Stopped { get; init; }

    public bool NeedsForceConfirmation { get; init; }

    public required string Message { get; init; }
}

public sealed class GameLoopSessionStopper
{
    public static readonly TimeSpan DefaultGrace = TimeSpan.FromSeconds(8);

    private readonly IProcessControl _processes;
    private readonly ISampleDelay _delay;
    private readonly IClock _clock;
    private readonly TimeSpan _grace;

    public GameLoopSessionStopper(IProcessControl processes, ISampleDelay delay, IClock clock)
        : this(processes, delay, clock, DefaultGrace)
    {
    }

    public GameLoopSessionStopper(IProcessControl processes, ISampleDelay delay, IClock clock, TimeSpan grace)
    {
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(delay);
        ArgumentNullException.ThrowIfNull(clock);
        _processes = processes;
        _delay = delay;
        _clock = clock;
        _grace = grace < TimeSpan.Zero ? TimeSpan.Zero : grace;
    }

    public async Task<StopResult> StopAsync(IReadOnlyList<string> installRoots, bool forceConfirmed, CancellationToken cancellationToken)
    {
        var targets = ProcessSelection.SelectStopTargets(_processes.List(), installRoots);
        if (targets.Count == 0)
        {
            return new StopResult { Stopped = true, Message = "GameLoop is not running." };
        }

        foreach (var target in targets)
        {
            if (StillInside(target.ProcessId, installRoots))
            {
                _processes.TryCloseMainWindow(target.ProcessId);
            }
        }

        var started = _clock.UtcNow;
        while (_clock.UtcNow - started < _grace)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ProcessSelection.SelectStopTargets(_processes.List(), installRoots).Count == 0)
            {
                return new StopResult { Stopped = true, Message = "GameLoop was asked to close." };
            }

            await _delay.WaitAsync(TimeSpan.FromMilliseconds(200), cancellationToken).ConfigureAwait(false);
        }

        var remaining = ProcessSelection.SelectStopTargets(_processes.List(), installRoots);
        if (remaining.Count == 0)
        {
            return new StopResult { Stopped = true, Message = "GameLoop was asked to close." };
        }

        if (!forceConfirmed)
        {
            return new StopResult { NeedsForceConfirmation = true, Message = ProcessStopMessages.ForceRequired };
        }

        foreach (var target in remaining)
        {
            if (StillInside(target.ProcessId, installRoots))
            {
                _processes.TryTerminate(target.ProcessId);
            }
        }

        var after = ProcessSelection.SelectStopTargets(_processes.List(), installRoots);
        return after.Count == 0
            ? new StopResult { Stopped = true, Message = "GameLoop processes inside the verified install were stopped." }
            : new StopResult { Message = "A verified GameLoop process did not stop." };
    }

    private bool StillInside(int processId, IReadOnlyList<string> installRoots)
    {
        foreach (var process in _processes.List())
        {
            if (process.ProcessId != processId)
            {
                continue;
            }

            return ProcessSelection.SelectStopTargets([process], installRoots).Count == 1;
        }

        return false;
    }
}

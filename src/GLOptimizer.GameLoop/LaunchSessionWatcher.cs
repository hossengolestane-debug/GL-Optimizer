using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Launch;
using GLOptimizer.Core.Logging;

namespace GLOptimizer.GameLoop;

/// <summary>
/// Clears the Launch Optimized journal after every recorded process has exited.
/// A journal that is still present at the next startup means the session did not finish.
/// </summary>
public sealed class LaunchSessionWatcher : IDisposable
{
    private readonly ILaunchJournalStore _journal;
    private readonly IProcessPriority _priority;
    private readonly ILogStore _log;
    private readonly CancellationTokenSource _cancellation = new();

    public LaunchSessionWatcher(ILaunchJournalStore journal, IProcessPriority priority, ILogStore log)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(priority);
        ArgumentNullException.ThrowIfNull(log);
        _journal = journal;
        _priority = priority;
        _log = log;
    }

    public void Start() => _ = RunAsync(_cancellation.Token);

    public void CheckOnce()
    {
        var journal = _journal.Load();
        if (journal is null)
        {
            return;
        }

        foreach (var change in journal.Changes)
        {
            if (_priority.IsRunning(change.ProcessId))
            {
                return;
            }
        }

        _journal.Clear();
        _log.Write(LogSeverity.Information, "Launch", "Launch Optimized ended because the recorded GameLoop processes exited.");
    }

    public void Dispose() => _cancellation.Cancel();

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                CheckOnce();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                // The next pass tries again. The journal stays until it can be cleared.
            }
        }
    }
}

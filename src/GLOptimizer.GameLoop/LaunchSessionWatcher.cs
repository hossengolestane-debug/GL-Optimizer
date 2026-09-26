using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Diagnostics;
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
    private int _watching;
    private int _disposed;

    public LaunchSessionWatcher(ILaunchJournalStore journal, IProcessPriority priority, ILogStore log)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(priority);
        ArgumentNullException.ThrowIfNull(log);
        _journal = journal;
        _priority = priority;
        _log = log;
    }

    public bool IsWatching => Volatile.Read(ref _watching) == 1;

    public void Start()
    {
        if (SmokeTest.Active)
        {
            return;
        }

        NoteSessionStarted();
    }

    public void NoteSessionStarted()
    {
        if (SmokeTest.Active)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _watching, 1, 0) != 0)
        {
            return;
        }

        if (_journal.Load() is null)
        {
            Volatile.Write(ref _watching, 0);
            return;
        }

        _ = RunAsync(_cancellation.Token);
    }

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

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _cancellation.Cancel();
        _cancellation.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
                    if (_journal.Load() is null)
                    {
                        return;
                    }

                    CheckOnce();
                    if (_journal.Load() is null)
                    {
                        return;
                    }
                }
                catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
                {
                    return;
                }
                catch (Exception)
                {
                    // The next pass tries again. The journal stays until it can be cleared.
                }
            }
        }
        finally
        {
            Volatile.Write(ref _watching, 0);
        }
    }
}

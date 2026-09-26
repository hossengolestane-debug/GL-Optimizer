using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Monitoring;
using GLOptimizer.Core.Settings;

namespace GLOptimizer.Monitoring;

public sealed class PerformanceSampler : IPerformanceSampler
{
    private readonly ISystemMonitor _system;
    private readonly IGameLoopMonitor _gameLoop;
    private readonly ISampleDelay _delay;
    private readonly RollingSampleBuffer _buffer;
    private readonly object _gate = new();
    private CancellationTokenSource? _cts;
    private int _generation;
    private int _interval = AppSettingsRules.DefaultSampleIntervalMilliseconds;

    public PerformanceSampler(ISystemMonitor system, IGameLoopMonitor gameLoop, ISampleDelay delay, RollingSampleBuffer? buffer = null)
    {
        ArgumentNullException.ThrowIfNull(system);
        ArgumentNullException.ThrowIfNull(gameLoop);
        ArgumentNullException.ThrowIfNull(delay);
        _system = system;
        _gameLoop = gameLoop;
        _delay = delay;
        _buffer = buffer ?? new RollingSampleBuffer();
    }

    public bool IsRunning { get; private set; }

    public int IntervalMilliseconds => _interval;

    public IReadOnlyList<MetricSample> History => _buffer.Snapshot();

    public event EventHandler<MetricSample>? Sampled;

    public void Start(int intervalMilliseconds)
    {
        var interval = AppSettingsRules.NormalizeSampleInterval(intervalMilliseconds);
        lock (_gate)
        {
            if (IsRunning && _interval == interval)
            {
                return;
            }

            CancelNoLock();
            _interval = interval;
            var generation = ++_generation;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            IsRunning = true;
            _ = Task.Run(() => LoopAsync(interval, token, generation));
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            CancelNoLock();
            IsRunning = false;
        }
    }

    public void Dispose() => Stop();

    private void CancelNoLock()
    {
        _generation++;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task LoopAsync(int intervalMilliseconds, CancellationToken token, int generation)
    {
        try
        {
            await _gameLoop.WarmAsync(token).ConfigureAwait(false);
            _ = SafeSystem();
            while (!token.IsCancellationRequested && generation == Volatile.Read(ref _generation))
            {
                await _delay.WaitAsync(TimeSpan.FromMilliseconds(intervalMilliseconds), token).ConfigureAwait(false);
                if (token.IsCancellationRequested || generation != Volatile.Read(ref _generation))
                {
                    break;
                }

                var now = DateTimeOffset.UtcNow;
                var system = SafeSystem();
                var game = SafeGame(now);
                var sample = new MetricSample
                {
                    Timestamp = now,
                    CpuPercent = system.CpuPercent,
                    GpuPercent = system.GpuPercent,
                    GpuVramUsedBytes = system.GpuVramUsedBytes,
                    RamPercent = system.RamPercent,
                    RamUsedBytes = system.RamUsedBytes,
                    RamTotalBytes = system.RamTotalBytes,
                    DiskPercent = system.DiskPercent,
                    GameLoopCpuPercent = game.CpuPercent,
                    GameLoopRamBytes = game.RamBytes,
                    GameLoopState = game.State
                };
                _buffer.Add(sample);
                try
                {
                    Sampled?.Invoke(this, sample);
                }
                catch (Exception)
                {
                    // A UI handler must not fault the sampler task.
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The delay or the install lookup was cancelled.
        }
        finally
        {
            lock (_gate)
            {
                if (generation == _generation)
                {
                    IsRunning = false;
                }
            }
        }
    }

    private SystemReading SafeSystem()
    {
        try
        {
            return _system.Read();
        }
        catch (Exception)
        {
            return new SystemReading();
        }
    }

    private GameLoopReading SafeGame(DateTimeOffset now)
    {
        try
        {
            return _gameLoop.Read(now);
        }
        catch (Exception)
        {
            return new GameLoopReading();
        }
    }
}

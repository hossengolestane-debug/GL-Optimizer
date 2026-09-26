using GLOptimizer.Core.Monitoring;
using GLOptimizer.Core.Navigation;

namespace GLOptimizer.Core.Abstractions;

public interface ISystemMonitor
{
    SystemReading Read();
}

public interface IGameLoopMonitor
{
    Task WarmAsync(CancellationToken cancellationToken);

    GameLoopReading Read(DateTimeOffset now);
}

public interface ISampleDelay
{
    Task WaitAsync(TimeSpan interval, CancellationToken cancellationToken);
}

public interface IPerformanceSampler : IDisposable
{
    bool IsRunning { get; }

    int IntervalMilliseconds { get; }

    IReadOnlyList<MetricSample> History { get; }

    event EventHandler<MetricSample>? Sampled;

    void Start(int intervalMilliseconds);

    void Stop();
}

public interface IMonitoringCoordinator : IDisposable
{
    bool IsRunning { get; }

    MetricSample? Latest { get; }

    IReadOnlyList<MetricSample> History { get; }

    PerformanceSummary Summary { get; }

    event EventHandler? Updated;

    void SetPage(AppPage page);

    void Start();

    void Stop();

    void NotifyIntervalChanged();

    void Refresh();
}

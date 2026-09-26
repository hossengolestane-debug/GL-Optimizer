using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Monitoring;
using GLOptimizer.Core.Navigation;
using GLOptimizer.Core.Settings;

namespace GLOptimizer.Monitoring;

public sealed class MonitoringCoordinator : IMonitoringCoordinator
{
    private readonly IPerformanceSampler _sampler;
    private readonly ISettingsStore _settings;
    private readonly object _gate = new();
    private AppPage _page;
    private bool _pageSet;
    private bool _held;

    public MonitoringCoordinator(IPerformanceSampler sampler, ISettingsStore settings)
    {
        ArgumentNullException.ThrowIfNull(sampler);
        ArgumentNullException.ThrowIfNull(settings);
        _sampler = sampler;
        _settings = settings;
        _sampler.Sampled += OnSampled;
    }

    public bool IsRunning => _sampler.IsRunning;

    public MetricSample? Latest { get; private set; }

    public IReadOnlyList<MetricSample> History => _sampler.History;

    public PerformanceSummary Summary => PerformanceHistory.Summarize(_sampler.History);

    public event EventHandler? Updated;

    public void SetPage(AppPage page)
    {
        lock (_gate)
        {
            _page = page;
            _pageSet = true;
            ApplyNoLock();
        }

        Updated?.Invoke(this, EventArgs.Empty);
    }

    public void Start()
    {
        lock (_gate)
        {
            _held = false;
            ApplyNoLock();
        }

        Updated?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        lock (_gate)
        {
            _held = true;
            if (_sampler.IsRunning)
            {
                _sampler.Stop();
            }
        }

        Updated?.Invoke(this, EventArgs.Empty);
    }

    public void Refresh()
    {
        lock (_gate)
        {
            ApplyNoLock();
        }

        Updated?.Invoke(this, EventArgs.Empty);
    }

    public void NotifyIntervalChanged()
    {
        lock (_gate)
        {
            if (_sampler.IsRunning)
            {
                _sampler.Start(CurrentInterval());
            }
        }

        Updated?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _sampler.Sampled -= OnSampled;
        Stop();
    }

    private void ApplyNoLock()
    {
        if (!_pageSet)
        {
            return;
        }

        var shouldRun = _settings.Current.MonitoringEnabled
            && (_page == AppPage.Dashboard || (_page == AppPage.Monitoring && !_held));
        if (!shouldRun)
        {
            if (_sampler.IsRunning)
            {
                _sampler.Stop();
            }

            return;
        }

        var interval = CurrentInterval();
        if (!_sampler.IsRunning || _sampler.IntervalMilliseconds != interval)
        {
            _sampler.Start(interval);
        }
    }

    private int CurrentInterval() =>
        AppSettingsRules.NormalizeSampleInterval(_settings.Current.SampleIntervalMilliseconds);

    private void OnSampled(object? sender, MetricSample sample)
    {
        Latest = sample;
        Updated?.Invoke(this, EventArgs.Empty);
    }
}

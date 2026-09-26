namespace GLOptimizer.Core.Monitoring;

public sealed class RollingSampleBuffer
{
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _window;
    private readonly int _maxCount;
    private readonly List<MetricSample> _samples = [];
    private readonly object _gate = new();

    public RollingSampleBuffer(TimeSpan? window = null, int maxCount = 720)
    {
        if (maxCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount));
        }

        _window = window is null || window.Value <= TimeSpan.Zero ? DefaultWindow : window.Value;
        _maxCount = maxCount;
    }

    public void Add(MetricSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        lock (_gate)
        {
            _samples.Add(sample);
            var cutoff = sample.Timestamp - _window;
            _samples.RemoveAll(existing => existing.Timestamp < cutoff);
            if (_samples.Count > _maxCount)
            {
                _samples.RemoveRange(0, _samples.Count - _maxCount);
            }
        }
    }

    public IReadOnlyList<MetricSample> Snapshot()
    {
        lock (_gate)
        {
            return _samples.ToArray();
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _samples.Clear();
        }
    }
}

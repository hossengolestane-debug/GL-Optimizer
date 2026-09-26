using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Monitoring;
using GLOptimizer.Core.Navigation;
using GLOptimizer.Core.Results;
using GLOptimizer.Core.Settings;
using GLOptimizer.Monitoring;

namespace GLOptimizer.Tests;

public class MonitoringLogicTests
{
    [Fact]
    public void Rolling_buffer_drops_samples_outside_the_window_and_caps_the_count()
    {
        var buffer = new RollingSampleBuffer(TimeSpan.FromMinutes(1), maxCount: 3);
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        buffer.Add(Sample(start, 1));
        buffer.Add(Sample(start.AddSeconds(30), 2));
        buffer.Add(Sample(start.AddSeconds(90), 3));

        var windowed = buffer.Snapshot();
        Assert.Equal(2, windowed.Count);
        Assert.Equal(2, windowed[0].CpuPercent);
        Assert.Equal(3, windowed[1].CpuPercent);

        buffer.Add(Sample(start.AddSeconds(91), 4));
        buffer.Add(Sample(start.AddSeconds(92), 5));
        var capped = buffer.Snapshot();
        Assert.Equal(3, capped.Count);
        Assert.Equal(5, capped[^1].CpuPercent);
    }

    [Fact]
    public void Spikes_fire_when_usage_crosses_90_and_not_while_it_stays_there()
    {
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var samples = new[]
        {
            Sample(start, cpu: 10, gpu: null),
            Sample(start.AddSeconds(1), cpu: 95, gpu: 10),
            Sample(start.AddSeconds(2), cpu: 97, gpu: 96),
            Sample(start.AddSeconds(3), cpu: 20, gpu: 97),
            Sample(start.AddSeconds(4), cpu: 91, gpu: null)
        };

        var spikes = PerformanceHistory.DetectSpikes(samples);
        Assert.Equal(3, spikes.Count);
        Assert.Equal("CPU", spikes[0].Metric);
        Assert.Equal(95, spikes[0].Value);
        Assert.Equal("GPU", spikes[1].Metric);
        Assert.Equal("CPU", spikes[2].Metric);

        var summary = PerformanceHistory.Summarize(samples);
        Assert.Equal((10 + 95 + 97 + 20 + 91) / 5d, summary.AverageCpuPercent);
        Assert.Equal(97, summary.PeakCpuPercent);
        Assert.Equal((10 + 96 + 97) / 3d, summary.AverageGpuPercent);
        Assert.Equal(97, summary.PeakGpuPercent);
        Assert.Null(summary.AverageRamPercent);
    }

    [Fact]
    public void Chart_series_caps_the_number_of_points()
    {
        var values = Enumerable.Range(0, 600).Select(index => (double?)index).ToArray();
        var series = ChartSeries.Downsample(values, 120);

        Assert.Equal(120, series.Length);
        Assert.False(double.IsNaN(series[0]));
        Assert.InRange(series[^1], 500, 599);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(double.NaN)]
    [InlineData(-1.0)]
    public void Invalid_percents_stay_unknown(double? value)
    {
        Assert.Null(MetricSanity.Percent(value));
    }

    [Fact]
    public void Percents_above_100_are_capped_instead_of_invented()
    {
        Assert.Equal(100, MetricSanity.Percent(140));
        Assert.Equal(12.5, MetricSanity.Percent(12.5));
    }

    [Fact]
    public void Frame_provider_does_not_invent_fps()
    {
        var result = new NotImplementedFrameMetricsProvider().TryGetLatest();

        Assert.Equal(OperationStatus.NotImplemented, result.Status);
        Assert.Null(result.Value);
        Assert.Equal("FPS monitoring unavailable with current safe monitoring method.", Phase0Notices.NoFrameMetrics);
        Assert.Equal("—", GLOptimizer.Core.Diagnostics.ReportedValue.FramesPerSecond(result));
    }

    [Fact]
    public async Task GameLoop_monitor_uses_only_processes_inside_the_install()
    {
        var root = Path.Combine(Path.GetTempPath(), "glopt-mon-" + Guid.NewGuid().ToString("N"));
        var inside = Path.Combine(root, "GameLoop.exe");
        var outside = Path.Combine(Path.GetTempPath(), "glopt-other-" + Guid.NewGuid().ToString("N") + ".exe");
        var probe = new ScriptedProbe();
        var monitor = new GameLoopMonitor(new FixedDetector(root), probe);
        await monitor.WarmAsync(CancellationToken.None);
        var start = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        probe.Result = new ProcessProbeResult
        {
            Available = true,
            Processes =
            [
                new ProbedProcess { ProcessId = 4, ExecutablePath = inside, TotalProcessorTime = TimeSpan.FromMilliseconds(1000), WorkingSetBytes = 4096 },
                new ProbedProcess { ProcessId = 8, ExecutablePath = outside, TotalProcessorTime = TimeSpan.FromMilliseconds(1000), WorkingSetBytes = 999 }
            ]
        };

        var first = monitor.Read(start);
        probe.Result = new ProcessProbeResult
        {
            Available = true,
            Processes =
            [
                new ProbedProcess
                {
                    ProcessId = 4,
                    ExecutablePath = inside,
                    TotalProcessorTime = TimeSpan.FromMilliseconds(1000 + 250),
                    WorkingSetBytes = 8192
                }
            ]
        };
        var second = monitor.Read(start.AddSeconds(1));

        Assert.Equal(GameRunStatus.Running, first.State);
        Assert.Null(first.CpuPercent);
        Assert.Equal(GameRunStatus.Running, second.State);
        Assert.Equal(8192, second.RamBytes);
        var expected = MetricSanity.Percent(250d / 1000d / Math.Max(1, Environment.ProcessorCount) * 100d);
        Assert.Equal(expected, second.CpuPercent);
    }

    [Fact]
    public async Task Empty_install_does_not_query_processes_or_claim_stopped()
    {
        var probe = new ScriptedProbe { ThrowOnCapture = true };
        var monitor = new GameLoopMonitor(new FixedDetector(null), probe);

        await monitor.WarmAsync(CancellationToken.None);
        var reading = monitor.Read(DateTimeOffset.UtcNow);

        Assert.Equal(GameRunStatus.Unknown, reading.State);
        Assert.Equal(0, probe.Captures);
    }

    [Fact]
    public async Task Unreadable_processes_stay_unknown()
    {
        var root = Path.Combine(Path.GetTempPath(), "glopt-mon-" + Guid.NewGuid().ToString("N"));
        var monitor = new GameLoopMonitor(new FixedDetector(root), new ScriptedProbe
        {
            Result = new ProcessProbeResult { Available = true, HadUnreadableMatch = true }
        });

        await monitor.WarmAsync(CancellationToken.None);

        Assert.Equal(GameRunStatus.Unknown, monitor.Read(DateTimeOffset.UtcNow).State);
    }

    [Fact]
    public async Task Sampler_publishes_on_the_delay_and_stops_without_another_sample()
    {
        var delay = new GateDelay();
        var system = new CountingSystem();
        var sampler = new PerformanceSampler(system, new IdleGame(), delay);
        var received = 0;
        sampler.Sampled += (_, _) => Interlocked.Increment(ref received);
        sampler.Start(1000);

        await WaitUntil(() => delay.Calls >= 1);
        delay.Release();
        await WaitUntil(() => Volatile.Read(ref received) >= 1);
        Assert.True(system.Reads < 5);

        sampler.Stop();
        delay.Release();
        await Task.Delay(50);

        Assert.False(sampler.IsRunning);
        Assert.Equal(1, Volatile.Read(ref received));
        sampler.Dispose();
        Assert.False(sampler.IsRunning);
    }

    [Fact]
    public void Coordinator_samples_only_while_dashboard_or_monitoring_is_active()
    {
        var sampler = new FakeSampler();
        var settings = new MemorySettings();
        using var coordinator = new MonitoringCoordinator(sampler, settings);

        coordinator.SetPage(AppPage.Dashboard);
        Assert.True(sampler.IsRunning);
        Assert.Equal(1, sampler.Starts);

        coordinator.SetPage(AppPage.Dashboard);
        Assert.Equal(1, sampler.Starts);

        coordinator.SetPage(AppPage.Settings);
        Assert.False(sampler.IsRunning);

        coordinator.SetPage(AppPage.Monitoring);
        Assert.True(sampler.IsRunning);

        coordinator.Stop();
        Assert.False(sampler.IsRunning);
        coordinator.SetPage(AppPage.Monitoring);
        Assert.False(sampler.IsRunning);

        coordinator.SetPage(AppPage.Dashboard);
        Assert.True(sampler.IsRunning);

        coordinator.SetPage(AppPage.Monitoring);
        Assert.False(sampler.IsRunning);

        coordinator.Start();
        Assert.True(sampler.IsRunning);
        settings.Value.SampleIntervalMilliseconds = 2000;
        coordinator.NotifyIntervalChanged();
        Assert.Equal(2000, sampler.IntervalMilliseconds);
    }

    private static MetricSample Sample(DateTimeOffset timestamp, double? cpu, double? gpu = null) => new()
    {
        Timestamp = timestamp,
        CpuPercent = cpu,
        GpuPercent = gpu
    };

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("The sampler did not reach the expected state.");
            }

            await Task.Delay(10);
        }
    }

    private sealed class FixedDetector : IGameLoopDetector
    {
        private readonly string? _root;

        public FixedDetector(string? root) => _root = root;

        public Task<OperationResult<GameLoopScan>> DetectAsync(CancellationToken cancellationToken = default)
        {
            var scan = new GameLoopScan();
            if (_root is not null)
            {
                scan = new GameLoopScan
                {
                    Installations = [new GameLoopInstallation { InstallPath = _root }]
                };
            }

            return Task.FromResult(OperationResult<GameLoopScan>.Success(scan));
        }
    }

    private sealed class ScriptedProbe : IProcessProbe
    {
        public ProcessProbeResult Result { get; set; } = new() { Available = true };

        public bool ThrowOnCapture { get; init; }

        public int Captures { get; private set; }

        public ProcessProbeResult Capture()
        {
            Captures++;
            if (ThrowOnCapture)
            {
                throw new InvalidOperationException("probe");
            }

            return Result;
        }
    }

    private sealed class CountingSystem : ISystemMonitor
    {
        public int Reads { get; private set; }

        public SystemReading Read()
        {
            Reads++;
            return new SystemReading { CpuPercent = 12 };
        }
    }

    private sealed class IdleGame : IGameLoopMonitor
    {
        public Task WarmAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public GameLoopReading Read(DateTimeOffset now) => new() { State = GameRunStatus.Stopped };
    }

    private sealed class GateDelay : ISampleDelay
    {
        private TaskCompletionSource _next = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls { get; private set; }

        public async Task WaitAsync(TimeSpan interval, CancellationToken cancellationToken)
        {
            var current = _next;
            Calls++;
            using var registration = cancellationToken.Register(() => current.TrySetCanceled(cancellationToken));
            await current.Task.ConfigureAwait(false);
        }

        public void Release()
        {
            var current = _next;
            _next = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            current.TrySetResult();
        }
    }

    private sealed class FakeSampler : IPerformanceSampler
    {
        public int Starts { get; private set; }

        public bool IsRunning { get; private set; }

        public int IntervalMilliseconds { get; private set; }

        public IReadOnlyList<MetricSample> History { get; } = [];

        public event EventHandler<MetricSample>? Sampled
        {
            add { }
            remove { }
        }

        public void Start(int intervalMilliseconds)
        {
            Starts++;
            IsRunning = true;
            IntervalMilliseconds = intervalMilliseconds;
        }

        public void Stop() => IsRunning = false;

        public void Dispose() => Stop();
    }

    private sealed class MemorySettings : ISettingsStore
    {
        public AppSettings Value { get; } = new();

        public AppSettings Current => Value;

        public OperationResult Load() => OperationResult.Success();

        public OperationResult Save(AppSettings settings)
        {
            Value.SampleIntervalMilliseconds = settings.SampleIntervalMilliseconds;
            return OperationResult.Success();
        }
    }
}

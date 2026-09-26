using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Monitoring;

namespace GLOptimizer.Monitoring;

public sealed class GameLoopMonitor : IGameLoopMonitor
{
    private readonly IGameLoopDetector _detector;
    private readonly IProcessProbe _probe;
    private readonly object _gate = new();
    private readonly Dictionary<int, (DateTimeOffset At, TimeSpan Cpu)> _previous = [];
    private IReadOnlyList<string> _roots = [];
    private bool _warmed;
    private DateTimeOffset _retryAfter = DateTimeOffset.MinValue;

    public GameLoopMonitor(IGameLoopDetector detector, IProcessProbe probe)
    {
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(probe);
        _detector = detector;
        _probe = probe;
    }

    public async Task WarmAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_warmed && (_roots.Count > 0 || DateTimeOffset.UtcNow < _retryAfter))
            {
                return;
            }
        }

        var scan = await _detector.DetectAsync(cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _warmed = true;
            _retryAfter = DateTimeOffset.UtcNow.AddMinutes(1);
            if (!scan.Succeeded || scan.Value is null)
            {
                _roots = [];
                return;
            }

            var roots = new List<string>();
            foreach (var installation in scan.Value.Installations)
            {
                if (!string.IsNullOrWhiteSpace(installation.InstallPath))
                {
                    roots.Add(installation.InstallPath);
                }
            }

            _roots = roots;
        }
    }

    public GameLoopReading Read(DateTimeOffset now)
    {
        lock (_gate)
        {
            return ReadNoLock(now);
        }
    }

    private GameLoopReading ReadNoLock(DateTimeOffset now)
    {
        if (_roots.Count == 0)
        {
            return new GameLoopReading { State = GameRunStatus.Unknown };
        }

        ProcessProbeResult probed;
        try
        {
            probed = _probe.Capture();
        }
        catch (Exception)
        {
            return new GameLoopReading { State = GameRunStatus.Unknown };
        }

        if (!probed.Available)
        {
            return new GameLoopReading { State = GameRunStatus.Unknown };
        }

        var matched = new List<ProbedProcess>();
        foreach (var process in probed.Processes)
        {
            if (IsInsideInstall(process.ExecutablePath))
            {
                matched.Add(process);
            }
        }

        var seen = new HashSet<int>();
        double cpuSum = 0;
        var cpuSamples = 0;
        long ram = 0;
        var ramSamples = 0;
        foreach (var process in matched)
        {
            seen.Add(process.ProcessId);
            var cpu = CpuPercent(process, now);
            if (cpu is not null)
            {
                cpuSum += cpu.Value;
                cpuSamples++;
            }

            if (MetricSanity.Bytes(process.WorkingSetBytes) is long bytes)
            {
                ram += bytes;
                ramSamples++;
            }
        }

        foreach (var id in _previous.Keys.Where(id => !seen.Contains(id)).ToArray())
        {
            _previous.Remove(id);
        }

        if (matched.Count == 0)
        {
            return new GameLoopReading
            {
                State = probed.HadUnreadableMatch ? GameRunStatus.Unknown : GameRunStatus.Stopped
            };
        }

        return new GameLoopReading
        {
            State = GameRunStatus.Running,
            CpuPercent = cpuSamples == 0 ? null : MetricSanity.Percent(cpuSum),
            RamBytes = ramSamples == 0 ? null : ram
        };
    }

    private bool IsInsideInstall(string? path)
    {
        foreach (var root in _roots)
        {
            if (InstallPathRules.IsUnderRoot(path, root))
            {
                return true;
            }
        }

        return false;
    }

    private double? CpuPercent(ProbedProcess process, DateTimeOffset now)
    {
        if (process.TotalProcessorTime is null)
        {
            return null;
        }

        if (!_previous.TryGetValue(process.ProcessId, out var prior))
        {
            _previous[process.ProcessId] = (now, process.TotalProcessorTime.Value);
            return null;
        }

        var wall = (now - prior.At).TotalMilliseconds;
        var cpu = (process.TotalProcessorTime.Value - prior.Cpu).TotalMilliseconds;
        _previous[process.ProcessId] = (now, process.TotalProcessorTime.Value);
        if (wall <= 0 || cpu < 0)
        {
            return null;
        }

        var cores = Math.Max(1, Environment.ProcessorCount);
        return MetricSanity.Percent(cpu / wall / cores * 100d);
    }
}

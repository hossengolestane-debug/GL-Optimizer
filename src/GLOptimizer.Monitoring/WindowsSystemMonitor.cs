using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Monitoring;

namespace GLOptimizer.Monitoring;

/// <summary>
/// Samples CPU, disk, and GPU through performance counters, and RAM through GlobalMemoryStatusEx.
/// Missing categories stay null. GPU instance lists refresh at most every 10 seconds.
/// </summary>
public sealed class WindowsSystemMonitor : ISystemMonitor, IDisposable
{
    private readonly object _gate = new();
    private PerformanceCounter? _cpu;
    private PerformanceCounter? _disk;
    private List<PerformanceCounter> _gpu = [];
    private List<PerformanceCounter> _vram = [];
    private List<string> _gpuNames = [];
    private List<string> _vramNames = [];
    private bool _cpuFailed;
    private bool _diskFailed;
    private bool _gpuPercentPending;
    private DateTimeOffset _nextGpuRefresh = DateTimeOffset.MinValue;

    public SystemReading Read()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new SystemReading();
        }

        lock (_gate)
        {
            return ReadOnWindows();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            DisposeCounter(_cpu);
            DisposeCounter(_disk);
            DisposeMany(_gpu);
            DisposeMany(_vram);
            _cpu = null;
            _disk = null;
            _gpu = [];
            _vram = [];
            _gpuNames = [];
            _vramNames = [];
        }
    }

    [SupportedOSPlatform("windows")]
    private SystemReading ReadOnWindows()
    {
        var memory = TryMemory();
        RefreshGpu();
        return new SystemReading
        {
            CpuPercent = MetricSanity.Percent(Next(_cpu, ref _cpuFailed, "Processor", "% Processor Time", "_Total")),
            DiskPercent = MetricSanity.Percent(Next(_disk, ref _diskFailed, "PhysicalDisk", "% Disk Time", "_Total")),
            GpuPercent = GpuPercent(),
            GpuVramUsedBytes = VramUsed(),
            RamPercent = memory.Percent,
            RamUsedBytes = memory.Used,
            RamTotalBytes = memory.Total
        };
    }

    [SupportedOSPlatform("windows")]
    private float? Next(PerformanceCounter? counter, ref bool failed, string category, string name, string instance)
    {
        if (failed)
        {
            return null;
        }

        try
        {
            counter ??= Create(category, name, instance);
            if (ReferenceEquals(counter, _cpu) || category == "Processor")
            {
                _cpu ??= counter;
            }

            if (category == "PhysicalDisk")
            {
                _disk ??= counter;
            }

            return counter.NextValue();
        }
        catch (Exception)
        {
            failed = true;
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static PerformanceCounter Create(string category, string name, string instance) =>
        new(category, name, instance, readOnly: true);

    [SupportedOSPlatform("windows")]
    private void RefreshGpu()
    {
        _gpuPercentPending = false;
        if (DateTimeOffset.UtcNow < _nextGpuRefresh)
        {
            return;
        }

        _nextGpuRefresh = DateTimeOffset.UtcNow.AddSeconds(10);
        try
        {
            var engineNames = EngineNames();
            if (!SameNames(_gpuNames, engineNames))
            {
                DisposeMany(_gpu);
                _gpu = [];
                _gpuNames = engineNames;
                foreach (var instance in engineNames)
                {
                    var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance, readOnly: true);
                    counter.NextValue();
                    _gpu.Add(counter);
                }

                _gpuPercentPending = _gpu.Count > 0;
            }

            var memoryNames = MemoryNames();
            if (!SameNames(_vramNames, memoryNames))
            {
                DisposeMany(_vram);
                _vram = [];
                _vramNames = memoryNames;
                foreach (var instance in memoryNames)
                {
                    _vram.Add(new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", instance, readOnly: true));
                }
            }

            if (_gpu.Count == 0 && _vram.Count == 0)
            {
                _nextGpuRefresh = DateTimeOffset.UtcNow.AddSeconds(60);
            }
        }
        catch (Exception)
        {
            _nextGpuRefresh = DateTimeOffset.UtcNow.AddSeconds(60);
            DisposeMany(_gpu);
            DisposeMany(_vram);
            _gpu = [];
            _vram = [];
            _gpuNames = [];
            _vramNames = [];
        }
    }

    [SupportedOSPlatform("windows")]
    private static List<string> EngineNames()
    {
        var names = new List<string>();
        var engines = new PerformanceCounterCategory("GPU Engine");
        if (!engines.CounterExists("Utilization Percentage"))
        {
            return names;
        }

        foreach (var instance in engines.GetInstanceNames())
        {
            if (!instance.Contains("engtype_3d", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            names.Add(instance);
            if (names.Count >= 48)
            {
                break;
            }
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }

    [SupportedOSPlatform("windows")]
    private static List<string> MemoryNames()
    {
        var names = new List<string>();
        var memory = new PerformanceCounterCategory("GPU Adapter Memory");
        if (!memory.CounterExists("Dedicated Usage"))
        {
            return names;
        }

        names.AddRange(memory.GetInstanceNames());
        names.Sort(StringComparer.Ordinal);
        return names;
    }

    private static bool SameNames(List<string> current, List<string> next)
    {
        if (current.Count != next.Count)
        {
            return false;
        }

        for (var index = 0; index < current.Count; index++)
        {
            if (!string.Equals(current[index], next[index], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    [SupportedOSPlatform("windows")]
    private double? GpuPercent()
    {
        if (_gpuPercentPending || _gpu.Count == 0)
        {
            return null;
        }

        double? max = null;
        foreach (var counter in _gpu)
        {
            try
            {
                var value = counter.NextValue();
                if (max is null || value > max)
                {
                    max = value;
                }
            }
            catch (Exception)
            {
                // One engine instance can disappear between refreshes.
            }
        }

        return MetricSanity.Percent(max);
    }

    [SupportedOSPlatform("windows")]
    private long? VramUsed()
    {
        if (_vram.Count == 0)
        {
            return null;
        }

        double sum = 0;
        var any = false;
        foreach (var counter in _vram)
        {
            try
            {
                sum += counter.NextValue();
                any = true;
            }
            catch (Exception)
            {
                // Skip an adapter that disappeared.
            }
        }

        if (!any || sum < 0 || double.IsNaN(sum) || double.IsInfinity(sum))
        {
            return null;
        }

        return (long)sum;
    }

    [SupportedOSPlatform("windows")]
    private static (double? Percent, long? Used, long? Total) TryMemory()
    {
        try
        {
            var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (!GlobalMemoryStatusEx(ref status) || status.TotalPhysical == 0)
            {
                return (null, null, null);
            }

            var total = (long)status.TotalPhysical;
            var available = (long)Math.Min(status.AvailablePhysical, status.TotalPhysical);
            var used = total - available;
            return (MetricSanity.Percent(used * 100d / total), used, total);
        }
        catch (Exception)
        {
            return (null, null, null);
        }
    }

    private static void DisposeCounter(PerformanceCounter? counter)
    {
        try
        {
            counter?.Dispose();
        }
        catch (Exception)
        {
            // Disposal is best-effort.
        }
    }

    private static void DisposeMany(List<PerformanceCounter> counters)
    {
        foreach (var counter in counters)
        {
            DisposeCounter(counter);
        }
    }

    [SupportedOSPlatform("windows")]
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }
}

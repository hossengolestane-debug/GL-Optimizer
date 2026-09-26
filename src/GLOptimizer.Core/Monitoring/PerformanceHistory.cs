namespace GLOptimizer.Core.Monitoring;

public sealed class PerformanceSpike
{
    public DateTimeOffset Timestamp { get; init; }

    public required string Metric { get; init; }

    public double Value { get; init; }
}

public sealed class PerformanceSummary
{
    public double? AverageCpuPercent { get; init; }

    public double? PeakCpuPercent { get; init; }

    public double? AverageGpuPercent { get; init; }

    public double? PeakGpuPercent { get; init; }

    public double? AverageRamPercent { get; init; }

    public IReadOnlyList<PerformanceSpike> Spikes { get; init; } = [];
}

public static class PerformanceHistory
{
    public const double SpikeThresholdPercent = 90;

    public static PerformanceSummary Summarize(IReadOnlyList<MetricSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var cpu = Numbers(samples, sample => sample.CpuPercent);
        var gpu = Numbers(samples, sample => sample.GpuPercent);
        var ram = Numbers(samples, sample => sample.RamPercent);
        return new PerformanceSummary
        {
            AverageCpuPercent = Average(cpu),
            PeakCpuPercent = Peak(cpu),
            AverageGpuPercent = Average(gpu),
            PeakGpuPercent = Peak(gpu),
            AverageRamPercent = Average(ram),
            Spikes = DetectSpikes(samples)
        };
    }

    public static IReadOnlyList<PerformanceSpike> DetectSpikes(IReadOnlyList<MetricSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var spikes = new List<PerformanceSpike>();
        double? previousCpu = null;
        double? previousGpu = null;
        foreach (var sample in samples)
        {
            if (Crossed(previousCpu, sample.CpuPercent))
            {
                spikes.Add(new PerformanceSpike
                {
                    Timestamp = sample.Timestamp,
                    Metric = "CPU",
                    Value = sample.CpuPercent!.Value
                });
            }

            if (Crossed(previousGpu, sample.GpuPercent))
            {
                spikes.Add(new PerformanceSpike
                {
                    Timestamp = sample.Timestamp,
                    Metric = "GPU",
                    Value = sample.GpuPercent!.Value
                });
            }

            if (sample.CpuPercent is not null)
            {
                previousCpu = sample.CpuPercent;
            }

            if (sample.GpuPercent is not null)
            {
                previousGpu = sample.GpuPercent;
            }
        }

        return spikes;
    }

    private static bool Crossed(double? previous, double? current) =>
        current is >= SpikeThresholdPercent && previous is not >= SpikeThresholdPercent;

    private static List<double> Numbers(IReadOnlyList<MetricSample> samples, Func<MetricSample, double?> select)
    {
        var values = new List<double>();
        foreach (var sample in samples)
        {
            if (select(sample) is double value)
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static double? Average(List<double> values) =>
        values.Count == 0 ? null : values.Average();

    private static double? Peak(List<double> values) =>
        values.Count == 0 ? null : values.Max();
}

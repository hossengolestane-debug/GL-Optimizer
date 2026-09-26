namespace GLOptimizer.Core.Monitoring;

public static class ChartSeries
{
    public const int DefaultPointLimit = 120;

    public static double[] Downsample(IReadOnlyList<double?> values, int maxPoints = DefaultPointLimit)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (maxPoints < 2)
        {
            maxPoints = 2;
        }

        if (values.Count == 0)
        {
            return [];
        }

        if (values.Count <= maxPoints)
        {
            var direct = new double[values.Count];
            for (var i = 0; i < values.Count; i++)
            {
                direct[i] = values[i] ?? double.NaN;
            }

            return direct;
        }

        var result = new double[maxPoints];
        var size = values.Count / (double)maxPoints;
        for (var bucket = 0; bucket < maxPoints; bucket++)
        {
            var start = (int)Math.Floor(bucket * size);
            var end = (int)Math.Floor((bucket + 1) * size);
            if (end <= start)
            {
                end = start + 1;
            }

            double sum = 0;
            var count = 0;
            for (var index = start; index < end && index < values.Count; index++)
            {
                if (values[index] is double sample && !double.IsNaN(sample))
                {
                    sum += sample;
                    count++;
                }
            }

            result[bucket] = count == 0 ? double.NaN : sum / count;
        }

        return result;
    }
}

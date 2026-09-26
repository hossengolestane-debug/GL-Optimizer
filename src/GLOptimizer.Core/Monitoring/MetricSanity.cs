namespace GLOptimizer.Core.Monitoring;

public static class MetricSanity
{
    public static double? Percent(double? value)
    {
        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value < 0)
        {
            return null;
        }

        return value.Value > 100 ? 100 : value.Value;
    }

    public static long? Bytes(long? value) => value is null or < 0 ? null : value;
}

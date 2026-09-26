using System.Globalization;
using GLOptimizer.Core.Detection;

namespace GLOptimizer.Core.Monitoring;

public static class MetricText
{
    public static string Percent(double? value) =>
        value is null ? HardwareText.Unknown : value.Value.ToString("0", CultureInfo.InvariantCulture) + "%";

    public static string RamPair(long? used, long? total)
    {
        if (used is null && total is null)
        {
            return HardwareText.Unknown;
        }

        if (used is null)
        {
            return HardwareText.Memory(total);
        }

        if (total is null)
        {
            return used <= 0 ? "0 GB" : HardwareText.Memory(used);
        }

        var usedText = used <= 0 ? "0 GB" : HardwareText.Memory(used);
        return usedText + " / " + HardwareText.Memory(total);
    }

    public static string Bytes(long? value) => value is 0 ? "0 GB" : HardwareText.Memory(value);
}

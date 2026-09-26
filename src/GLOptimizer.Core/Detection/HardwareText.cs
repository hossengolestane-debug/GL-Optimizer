using System.Globalization;

namespace GLOptimizer.Core.Detection;

public static class HardwareText
{
    public const string Unknown = "Unknown";

    public static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? Unknown : value.Trim();

    public static string Count(int? value) =>
        value is > 0 ? value.Value.ToString(CultureInfo.InvariantCulture) : Unknown;

    public static string Memory(long? bytes)
    {
        if (bytes is null or <= 0)
        {
            return Unknown;
        }

        var gigabytes = bytes.Value / 1024d / 1024d / 1024d;
        return gigabytes.ToString("0.0", CultureInfo.InvariantCulture) + " GB";
    }

    public static string Hertz(int? hz) =>
        hz is null ? Unknown : hz.Value.ToString(CultureInfo.InvariantCulture) + " Hz";

    public static string Flag(bool? value, string yes, string no) => value switch
    {
        true => yes,
        false => no,
        _ => Unknown
    };
}

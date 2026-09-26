using System.Globalization;

namespace GLOptimizer.Core.Logging;

public static class LogArchiveNames
{
    public const string Prefix = "gloptimizer-";
    public const string StampFormat = "yyyyMMdd-HHmmssfff";

    public static string Create(DateTimeOffset utcNow, int suffix = 0)
    {
        var stamp = utcNow.UtcDateTime.ToString(StampFormat, CultureInfo.InvariantCulture);
        var name = suffix <= 0
            ? $"{Prefix}{stamp}.log"
            : $"{Prefix}{stamp}-{suffix}.log";
        return name;
    }

    public static bool TryReadStamp(string fileName, out DateTimeOffset stamp)
    {
        stamp = default;
        var name = Path.GetFileNameWithoutExtension(fileName);
        if (!name.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var body = name[Prefix.Length..];
        var stampText = body;
        if (body.Count(static c => c == '-') >= 2)
        {
            stampText = body[..body.LastIndexOf('-')];
        }

        if (!DateTime.TryParseExact(
                stampText,
                StampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return false;
        }

        stamp = new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Utc));
        return true;
    }

    public static bool IsExpired(string fileName, DateTimeOffset utcNow, int retentionDays)
    {
        if (retentionDays < 1 || !TryReadStamp(fileName, out var stamp))
        {
            return false;
        }

        return stamp < utcNow.AddDays(-retentionDays);
    }
}

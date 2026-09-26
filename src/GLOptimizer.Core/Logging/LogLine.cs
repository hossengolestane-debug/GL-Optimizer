using System.Globalization;
using System.Text;

namespace GLOptimizer.Core.Logging;

/// <summary>
/// Tab-separated log lines. Tabs and newlines inside fields are escaped so a line stays one record.
/// </summary>
public static class LogLine
{
    public static string Format(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var timestamp = entry.Timestamp.ToString("O", CultureInfo.InvariantCulture);
        return string.Join(
            '\t',
            timestamp,
            entry.Severity.ToString(),
            Escape(entry.Category),
            Escape(entry.Message),
            Escape(entry.Exception ?? string.Empty));
    }

    public static bool TryParse(string? line, out LogEntry? entry)
    {
        entry = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var parts = line.Split('\t');
        if (parts.Length != 5)
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
        {
            return false;
        }

        if (!Enum.TryParse<LogSeverity>(parts[1], ignoreCase: false, out var severity) || !Enum.IsDefined(severity))
        {
            return false;
        }

        var category = Unescape(parts[2]);
        if (string.IsNullOrWhiteSpace(category))
        {
            return false;
        }

        var exceptionText = Unescape(parts[4]);
        entry = new LogEntry(
            timestamp,
            severity,
            category,
            Unescape(parts[3]),
            exceptionText.Length == 0 ? null : exceptionText);
        return true;
    }

    public static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);
    }

    public static string Unescape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                var next = value[++i];
                builder.Append(next switch
                {
                    '\\' => '\\',
                    'r' => '\r',
                    'n' => '\n',
                    't' => '\t',
                    _ => next
                });
                continue;
            }

            builder.Append(value[i]);
        }

        return builder.ToString();
    }
}

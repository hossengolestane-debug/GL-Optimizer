using System.Text.RegularExpressions;

namespace GLOptimizer.Core.Detection;

/// <summary>
/// Accepts numeric versions with 2–4 parts. Words such as "latest" are rejected. Conflicting values become null.
/// </summary>
public static partial class VersionText
{
    private static readonly string[] Keys = ["versionName", "DisplayVersion", "AppVersion", "Version"];

    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim().Trim('"').Trim();
        if (text.Length > 0 && (text[0] == 'v' || text[0] == 'V'))
        {
            text = text[1..].TrimStart();
        }

        var end = 0;
        while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '.'))
        {
            end++;
        }

        if (end == 0)
        {
            return null;
        }

        text = text[..end].TrimEnd('.');
        return VersionPattern().IsMatch(text) ? text : null;
    }

    public static string? FromContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rawLine in content.Split(['\r', '\n']))
        {
            var version = ReadLine(rawLine);
            if (version is null)
            {
                continue;
            }

            found.Add(version);
            if (found.Count > 1)
            {
                return null;
            }
        }

        return found.Count == 1 ? found.First() : null;
    }

    private static string? ReadLine(string rawLine)
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line[0] is '#' or ';')
        {
            return null;
        }

        var separator = line.IndexOfAny([':', '=']);
        if (separator <= 0)
        {
            return Normalize(line);
        }

        var key = line[..separator].Trim().Trim('"');
        if (!IsVersionKey(key))
        {
            return null;
        }

        var value = line[(separator + 1)..].Trim().TrimEnd(',').Trim().Trim('"');
        return Normalize(value);
    }

    private static bool IsVersionKey(string key)
    {
        foreach (var candidate in Keys)
        {
            if (key.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"^\d{1,5}(\.\d{1,5}){1,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}

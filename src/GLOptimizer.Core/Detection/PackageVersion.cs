using System.Globalization;
using System.Text.RegularExpressions;

namespace GLOptimizer.Core.Detection;

/// <summary>
/// A 2–4 part numeric version. Prerelease text and trailing words are rejected, not stripped.
/// </summary>
public sealed partial class PackageVersion : IComparable<PackageVersion>
{
    private PackageVersion(int[] parts, string text)
    {
        Parts = parts;
        Text = text;
    }

    public IReadOnlyList<int> Parts { get; }

    public string Text { get; }

    public static PackageVersion? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim().Trim('"').Trim();
        if (text.Length > 1 && (text[0] == 'v' || text[0] == 'V') && char.IsDigit(text[1]))
        {
            text = text[1..].TrimStart();
        }

        if (!VersionPattern().IsMatch(text))
        {
            return null;
        }

        var tokens = text.Split('.');
        var parts = new int[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
        {
            if (!int.TryParse(tokens[i], NumberStyles.None, CultureInfo.InvariantCulture, out parts[i]))
            {
                return null;
            }
        }

        return new PackageVersion(parts, text);
    }

    /// <summary>
    /// Reads one unambiguous version from text. Two different versions, or a prerelease value, return null.
    /// </summary>
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

            found.Add(version.Text);
            if (found.Count > 1)
            {
                return null;
            }
        }

        return found.Count == 1 ? found.First() : null;
    }

    public int CompareTo(PackageVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var count = Math.Max(Parts.Count, other.Parts.Count);
        for (var i = 0; i < count; i++)
        {
            var left = i < Parts.Count ? Parts[i] : 0;
            var right = i < other.Parts.Count ? other.Parts[i] : 0;
            if (left != right)
            {
                return left.CompareTo(right);
            }
        }

        return 0;
    }

    private static PackageVersion? ReadLine(string rawLine)
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line[0] is '#' or ';')
        {
            return null;
        }

        var separator = line.IndexOfAny([':', '=']);
        if (separator <= 0)
        {
            return Parse(line);
        }

        var key = line[..separator].Trim().Trim('"');
        if (!IsVersionKey(key))
        {
            return null;
        }

        var value = line[(separator + 1)..].Trim().TrimEnd(',').Trim();
        return Parse(value);
    }

    private static bool IsVersionKey(string key)
    {
        return key.Equals("versionName", StringComparison.OrdinalIgnoreCase)
            || key.Equals("DisplayVersion", StringComparison.OrdinalIgnoreCase)
            || key.Equals("AppVersion", StringComparison.OrdinalIgnoreCase)
            || key.Equals("Version", StringComparison.OrdinalIgnoreCase)
            || key.Equals("version_name", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"^\d{1,5}(\.\d{1,5}){1,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();
}

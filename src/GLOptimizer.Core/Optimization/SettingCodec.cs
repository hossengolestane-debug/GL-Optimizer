using System.Globalization;

namespace GLOptimizer.Core.Optimization;

public static class SettingCodec
{
    public static string? NewRaw(string setting, string key, string currentRaw, string recommended)
    {
        if (string.IsNullOrWhiteSpace(currentRaw) || string.IsNullOrWhiteSpace(recommended))
        {
            return null;
        }

        return setting switch
        {
            "Resolution" => Resolution(key, currentRaw, recommended),
            "MemoryAllocation" => WithSuffix(currentRaw, Digits(recommended), "MB"),
            "Dpi" => WithSuffix(currentRaw, recommended, "DPI"),
            "CpuAllocation" or "FpsTarget" => recommended,
            "VSync" => SwitchWord(currentRaw, recommended == "On"),
            "AntiAliasing" => AntiAlias(currentRaw, recommended),
            "Renderer" => MatchCase(currentRaw, recommended),
            _ => null
        };
    }

    private static string? Resolution(string key, string currentRaw, string recommended)
    {
        var parts = recommended.Split('×');
        if (parts.Length != 2)
        {
            return null;
        }

        if (key.Equals("VMResWidth", StringComparison.OrdinalIgnoreCase))
        {
            return parts[0];
        }

        if (key.Equals("VMResHeight", StringComparison.OrdinalIgnoreCase))
        {
            return parts[1];
        }

        var separator = currentRaw.Contains('×') ? "×"
            : currentRaw.Contains('*') ? "*"
            : currentRaw.Contains('X') ? "X"
            : "x";
        return parts[0] + separator + parts[1];
    }

    private static string WithSuffix(string currentRaw, string number, string suffix)
    {
        var trimmed = currentRaw.Trim();
        if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            var gap = trimmed.Length > suffix.Length && trimmed[trimmed.Length - suffix.Length - 1] == ' ' ? " " : string.Empty;
            var token = trimmed[..^suffix.Length].TrimEnd();
            if (token.Length == 0)
            {
                return number;
            }

            return number + gap + MatchCase(trimmed[^suffix.Length..], suffix);
        }

        return number;
    }

    private static string Digits(string recommended)
    {
        var space = recommended.IndexOf(' ');
        return space < 0 ? recommended : recommended[..space];
    }

    private static string? SwitchWord(string currentRaw, bool on)
    {
        var token = currentRaw.Trim();
        return token.ToLowerInvariant() switch
        {
            "1" or "0" => on ? "1" : "0",
            "true" or "false" => MatchCase(token, on ? "true" : "false"),
            "yes" or "no" => MatchCase(token, on ? "yes" : "no"),
            "on" or "off" => MatchCase(token, on ? "on" : "off"),
            _ => null
        };
    }

    private static string? AntiAlias(string currentRaw, string recommended)
    {
        var token = currentRaw.Trim();
        if (int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            return recommended switch
            {
                "Off" => "0",
                "Balanced" => "2",
                "Ultra" => "4",
                _ => null
            };
        }

        if (token.Equals("Off", StringComparison.OrdinalIgnoreCase)
            || token.Equals("Balanced", StringComparison.OrdinalIgnoreCase)
            || token.Equals("Ultra", StringComparison.OrdinalIgnoreCase))
        {
            return MatchCase(token, recommended);
        }

        return null;
    }

    private static string MatchCase(string sample, string canonical)
    {
        var letters = sample.Where(char.IsLetter).ToArray();
        if (letters.Length > 0 && letters.All(char.IsUpper))
        {
            return canonical.ToUpperInvariant();
        }

        if (letters.Length > 0 && letters.All(char.IsLower))
        {
            return canonical.ToLowerInvariant();
        }

        return canonical;
    }
}

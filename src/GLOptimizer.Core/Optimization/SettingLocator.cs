using System.Globalization;
using GLOptimizer.Core.Configuration;

namespace GLOptimizer.Core.Optimization;

public static class SettingLocator
{
    private static readonly string[] SettingNames =
    [
        "Renderer",
        "Resolution",
        "Dpi",
        "MemoryAllocation",
        "CpuAllocation",
        "VSync",
        "AntiAliasing",
        "FpsTarget"
    ];

    public static IReadOnlyList<string> Names => SettingNames;

    public static IReadOnlyList<LocatedSetting> Locate(string path, string format, IReadOnlyList<ConfigPair> pairs, bool fromRegistry)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        var parsed = GameLoopSettingsParser.Parse(pairs);
        var located = new List<LocatedSetting>();
        foreach (var name in SettingNames)
        {
            var keys = Keys(name, pairs);
            if (keys.Count == 0)
            {
                continue;
            }

            var normalized = Value(parsed, name);
            var valid = normalized is not null && keys.All(key => Contributes(name, key, normalized));
            located.Add(new LocatedSetting
            {
                Name = name,
                Path = path,
                Format = format,
                Normalized = valid ? normalized : null,
                FromRegistry = fromRegistry,
                Valid = valid,
                Keys = keys
            });
        }

        return located;
    }

    private static List<RawSettingKey> Keys(string setting, IReadOnlyList<ConfigPair> pairs)
    {
        var keys = new List<RawSettingKey>();
        foreach (var pair in pairs)
        {
            if (IsKey(setting, pair.Key))
            {
                keys.Add(new RawSettingKey { Name = pair.Key, Raw = pair.Value ?? string.Empty });
            }
        }

        return keys;
    }

    private static bool IsKey(string setting, string key) => setting switch
    {
        "Renderer" => key.Equals("Renderer", StringComparison.OrdinalIgnoreCase)
            || key.Equals("RendererMode", StringComparison.OrdinalIgnoreCase)
            || key.Equals("ScreenRenderingMode", StringComparison.OrdinalIgnoreCase),
        "Resolution" => key.Equals("Resolution", StringComparison.OrdinalIgnoreCase)
            || key.Equals("VMResWidth", StringComparison.OrdinalIgnoreCase)
            || key.Equals("VMResHeight", StringComparison.OrdinalIgnoreCase),
        "Dpi" => key.Equals("VMDPI", StringComparison.OrdinalIgnoreCase) || key.Equals("DPI", StringComparison.OrdinalIgnoreCase),
        "MemoryAllocation" => key.Equals("VMMemorySizeInMB", StringComparison.OrdinalIgnoreCase)
            || key.Equals("MemoryAllocation", StringComparison.OrdinalIgnoreCase),
        "CpuAllocation" => key.Equals("VMCpuCount", StringComparison.OrdinalIgnoreCase)
            || key.Equals("CpuAllocation", StringComparison.OrdinalIgnoreCase),
        "VSync" => key.Equals("VSyncEnabled", StringComparison.OrdinalIgnoreCase) || key.Equals("VSync", StringComparison.OrdinalIgnoreCase),
        "AntiAliasing" => key.Equals("FxaaQuality", StringComparison.OrdinalIgnoreCase)
            || key.Equals("AntiAliasing", StringComparison.OrdinalIgnoreCase),
        "FpsTarget" => GameLoopSettingsParser.IsFpsKey(key),
        _ => false
    };

    private static bool Contributes(string setting, RawSettingKey key, string normalized)
    {
        if (setting == "Resolution")
        {
            if (key.Name.Equals("VMResWidth", StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(Digits(key.Raw), NumberStyles.None, CultureInfo.InvariantCulture, out var width)
                    && width.ToString(CultureInfo.InvariantCulture) == Width(normalized);
            }

            if (key.Name.Equals("VMResHeight", StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(Digits(key.Raw), NumberStyles.None, CultureInfo.InvariantCulture, out var height)
                    && height.ToString(CultureInfo.InvariantCulture) == Height(normalized);
            }
        }

        return true;
    }

    private static string? Value(GameLoopSettings settings, string name) => name switch
    {
        "Renderer" => settings.Renderer,
        "Resolution" => settings.Resolution,
        "Dpi" => settings.Dpi,
        "MemoryAllocation" => settings.MemoryAllocation,
        "CpuAllocation" => settings.CpuAllocation,
        "VSync" => settings.VSync,
        "AntiAliasing" => settings.AntiAliasing,
        "FpsTarget" => settings.FpsTarget,
        _ => null
    };

    private static string Width(string resolution) => resolution.Split('×')[0];

    private static string Height(string resolution)
    {
        var parts = resolution.Split('×');
        return parts.Length == 2 ? parts[1] : string.Empty;
    }

    private static string Digits(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[..^2].Trim();
        }

        if (trimmed.EndsWith("DPI", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed[..^3].Trim();
        }

        return trimmed;
    }
}

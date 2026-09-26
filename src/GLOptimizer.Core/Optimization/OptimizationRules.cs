using System.Globalization;
using GLOptimizer.Core.Models;

namespace GLOptimizer.Core.Optimization;

public static class OptimizationRules
{
    public static IReadOnlyList<OptimizationRecommendation> Build(
        HardwareTier tier,
        OptimizationProfile profile,
        HardwareReport? hardware,
        IReadOnlyList<LocatedSetting> located)
    {
        ArgumentNullException.ThrowIfNull(located);
        var rows = new List<OptimizationRecommendation>();
        foreach (var name in SettingLocator.Names)
        {
            rows.Add(Recommend(name, tier, profile, hardware, located));
        }

        return rows;
    }

    private static OptimizationRecommendation Recommend(
        string setting,
        HardwareTier tier,
        OptimizationProfile profile,
        HardwareReport? hardware,
        IReadOnlyList<LocatedSetting> located)
    {
        var matches = located.Where(item => item.Name == setting).ToArray();
        if (matches.Length == 0)
        {
            return Skip(setting, null, null, "The key was not found in a parsed configuration file.");
        }

        if (matches.Any(item => !item.Valid || item.Normalized is null))
        {
            return Skip(setting, null, null, "The current value is not valid, so it was not changed.");
        }

        var fileMatches = matches.Where(item => !item.FromRegistry).ToArray();
        if (fileMatches.Length == 0)
        {
            return NotSupported(setting, matches[0].Normalized, "Registry-backed settings are not written in this phase.");
        }

        var current = fileMatches[0].Normalized!;
        if (fileMatches.Any(item => !string.Equals(item.Normalized, current, StringComparison.Ordinal)))
        {
            return Skip(setting, current, null, "The same setting has different values, so it was not changed.");
        }

        if (profile == OptimizationProfile.Custom)
        {
            return Skip(setting, current, null, "Custom profile does not select values.");
        }

        var recommended = Target(setting, tier, profile, hardware, current);
        if (recommended is null)
        {
            return Skip(setting, current, null, ReasonForSkip(setting, tier));
        }

        if (Same(setting, current, recommended))
        {
            return Optimal(setting, current, recommended);
        }

        var edits = new List<OptimizationEdit>();
        foreach (var file in fileMatches)
        {
            foreach (var key in file.Keys)
            {
                var raw = SettingCodec.NewRaw(setting, key.Name, key.Raw, recommended);
                if (raw is null)
                {
                    return Skip(setting, current, recommended, "The current value cannot be replaced without changing the file format.");
                }

                if (string.Equals(raw, key.Raw.Trim(), StringComparison.Ordinal))
                {
                    continue;
                }

                edits.Add(new OptimizationEdit
                {
                    Setting = setting,
                    Path = file.Path,
                    Format = file.Format,
                    Key = key.Name,
                    CurrentRaw = key.Raw.Trim(),
                    NewRaw = raw
                });
            }
        }

        if (edits.Count == 0)
        {
            return Optimal(setting, current, recommended);
        }

        var raising = Raises(setting, current, recommended);
        return new OptimizationRecommendation
        {
            Setting = setting,
            CurrentValue = current,
            RecommendedValue = recommended,
            Reason = Reason(setting, tier, profile, raising),
            Risk = setting == "Renderer" || raising ? RiskLevel.Medium : RiskLevel.Low,
            RequiresRestart = true,
            Reversible = true,
            Status = RecommendationStatus.Applicable,
            Edits = edits
        };
    }

    private static string? Target(string setting, HardwareTier tier, OptimizationProfile profile, HardwareReport? hardware, string current)
    {
        if (setting == "Renderer")
        {
            if (current.Equals("OpenGL+", StringComparison.OrdinalIgnoreCase))
            {
                return "OpenGL";
            }

            if (current.Equals("DirectX+", StringComparison.OrdinalIgnoreCase))
            {
                return "DirectX";
            }

            return current;
        }

        if (tier == HardwareTier.Unknown)
        {
            return Conservative(setting, hardware, current);
        }

        return setting switch
        {
            "Resolution" => Resolution(tier, profile),
            "Dpi" => tier == HardwareTier.Low ? "160" : "240",
            "AntiAliasing" => AntiAlias(tier, profile),
            "FpsTarget" => Fps(tier, profile),
            "VSync" => profile == OptimizationProfile.Performance ? "Off" : "On",
            "CpuAllocation" => Cpu(profile, hardware, current, allowIncrease: true),
            "MemoryAllocation" => Memory(profile, hardware, current, allowIncrease: true),
            _ => null
        };
    }

    private static string? Conservative(string setting, HardwareReport? hardware, string current)
    {
        switch (setting)
        {
            case "Resolution":
                return Height(current) > 900 ? "1280×720" : current;
            case "Dpi":
                return int.TryParse(current, NumberStyles.None, CultureInfo.InvariantCulture, out var dpi) && dpi > 240 ? "160" : current;
            case "AntiAliasing":
                return MeansUltra(current) ? "Balanced" : current;
            case "FpsTarget":
                return int.TryParse(current, NumberStyles.None, CultureInfo.InvariantCulture, out var fps) && fps > 60 ? "30" : current;
            case "CpuAllocation":
                return Cpu(OptimizationProfile.Balanced, hardware, current, allowIncrease: false);
            case "MemoryAllocation":
                return Memory(OptimizationProfile.Balanced, hardware, current, allowIncrease: false);
            default:
                return null;
        }
    }

    private static string? Cpu(OptimizationProfile profile, HardwareReport? hardware, string current, bool allowIncrease)
    {
        var cap = HardwareTierClassifier.CpuHeadroom(hardware?.LogicalCores);
        if (cap is null || !int.TryParse(current, NumberStyles.None, CultureInfo.InvariantCulture, out var now))
        {
            return null;
        }

        if (now > cap)
        {
            return cap.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (allowIncrease && profile == OptimizationProfile.Performance && now < cap)
        {
            return cap.Value.ToString(CultureInfo.InvariantCulture);
        }

        return current;
    }

    private static string? Memory(OptimizationProfile profile, HardwareReport? hardware, string current, bool allowIncrease)
    {
        var cap = HardwareTierClassifier.MemoryHeadroomMb(hardware?.TotalMemoryBytes);
        var digits = current.Split(' ')[0];
        if (cap is null || !int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var now))
        {
            return null;
        }

        if (now > cap)
        {
            return cap.Value.ToString(CultureInfo.InvariantCulture) + " MB";
        }

        if (allowIncrease && profile == OptimizationProfile.Performance && now < cap)
        {
            return cap.Value.ToString(CultureInfo.InvariantCulture) + " MB";
        }

        return current;
    }

    private static string Resolution(HardwareTier tier, OptimizationProfile profile) => (tier, profile) switch
    {
        (HardwareTier.Low, OptimizationProfile.Performance) => "1280×720",
        (HardwareTier.Low, _) => "1600×900",
        (HardwareTier.Mid, OptimizationProfile.Performance) => "1600×900",
        (HardwareTier.Mid, _) => "1920×1080",
        (HardwareTier.High, OptimizationProfile.Quality) => "2560×1440",
        _ => "1920×1080"
    };

    private static string AntiAlias(HardwareTier tier, OptimizationProfile profile)
    {
        if (profile == OptimizationProfile.Performance)
        {
            return "Off";
        }

        if (profile == OptimizationProfile.Balanced || tier == HardwareTier.Low)
        {
            return "Balanced";
        }

        return "Ultra";
    }

    private static string Fps(HardwareTier tier, OptimizationProfile profile) => (tier, profile) switch
    {
        (HardwareTier.Low, OptimizationProfile.Performance) => "60",
        (HardwareTier.Low, OptimizationProfile.Balanced) => "40",
        (HardwareTier.Low, _) => "30",
        (HardwareTier.Mid, OptimizationProfile.Quality) => "40",
        (HardwareTier.Mid, _) => "60",
        (HardwareTier.High, OptimizationProfile.Performance) => "90",
        _ => "60"
    };

    private static bool Same(string setting, string current, string recommended)
    {
        if (setting == "AntiAliasing")
        {
            return AntiAliasRank(current) == AntiAliasRank(recommended);
        }

        if (setting == "MemoryAllocation")
        {
            return string.Equals(current.Split(' ')[0], recommended.Split(' ')[0], StringComparison.Ordinal);
        }

        return string.Equals(current, recommended, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Raises(string setting, string current, string recommended)
    {
        if (setting is "Resolution")
        {
            return Height(recommended) > Height(current);
        }

        if (setting is "Dpi" or "CpuAllocation" or "FpsTarget" or "MemoryAllocation")
        {
            return Number(recommended) > Number(current);
        }

        if (setting == "AntiAliasing")
        {
            return AntiAliasRank(recommended) > AntiAliasRank(current);
        }

        return false;
    }

    private static int Height(string resolution)
    {
        var parts = resolution.Split(['×', 'x', 'X', '*'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var height) ? height : 0;
    }

    private static int Number(string value)
    {
        var digits = value.Split(' ')[0];
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    private static int AntiAliasRank(string value) => value.ToLowerInvariant() switch
    {
        "off" or "0" => 0,
        "balanced" or "1" or "2" => 1,
        "ultra" or "3" or "4" or "5" or "6" or "7" or "8" => 2,
        _ => -1
    };

    private static bool MeansUltra(string value) => AntiAliasRank(value) == 2;

    private static string Reason(string setting, HardwareTier tier, OptimizationProfile profile, bool raising)
    {
        if (setting == "Renderer")
        {
            return "Compatibility-based. A plus renderer mode is stepped back to the base mode. This does not claim a higher frame rate.";
        }

        if (setting is "CpuAllocation" or "MemoryAllocation")
        {
            return "Leaves headroom for Windows. GameLoop is not given every processor or almost all of the memory.";
        }

        var direction = raising ? "Raises" : "Lowers";
        return direction + " this value for the " + profile + " profile on " + TierName(tier) + " hardware. This does not claim a higher frame rate.";
    }

    private static string ReasonForSkip(string setting, HardwareTier tier)
    {
        if (tier == HardwareTier.Unknown && setting is "VSync" or "Renderer")
        {
            return "Unknown hardware does not change this setting.";
        }

        if (setting is "CpuAllocation")
        {
            return "CPU headroom cannot be calculated, so the processor count was not changed.";
        }

        if (setting is "MemoryAllocation")
        {
            return "Memory headroom cannot leave several GB for Windows, so the allocation was not changed.";
        }

        return "This setting was left unchanged.";
    }

    private static string TierName(HardwareTier tier) => tier switch
    {
        HardwareTier.Low => "low-end",
        HardwareTier.Mid => "mid-range",
        HardwareTier.High => "high-end",
        _ => "unknown"
    };

    private static OptimizationRecommendation Optimal(string setting, string current, string recommended) => new()
    {
        Setting = setting,
        CurrentValue = current,
        RecommendedValue = recommended,
        Reason = "Already at the recommended value.",
        Risk = RiskLevel.Low,
        RequiresRestart = false,
        Reversible = false,
        Status = RecommendationStatus.AlreadyOptimal
    };

    private static OptimizationRecommendation Skip(string setting, string? current, string? recommended, string reason) => new()
    {
        Setting = setting,
        CurrentValue = current,
        RecommendedValue = recommended,
        Reason = reason,
        Risk = RiskLevel.Low,
        Status = RecommendationStatus.Skipped
    };

    private static OptimizationRecommendation NotSupported(string setting, string? current, string reason) => new()
    {
        Setting = setting,
        CurrentValue = current,
        RecommendedValue = null,
        Reason = reason,
        Risk = RiskLevel.Medium,
        Status = RecommendationStatus.NotSupported
    };
}

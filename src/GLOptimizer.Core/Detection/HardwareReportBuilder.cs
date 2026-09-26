using GLOptimizer.Core.Models;

namespace GLOptimizer.Core.Detection;

public static class HardwareReportBuilder
{
    public const long RejectedVramAtOrAbove = 0xFFFF0000L;

    public static HardwareReport Build(HardwareProbeSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return new HardwareReport();
        }

        var (gpuName, vram) = DescribeGpus(snapshot.Gpus);
        return new HardwareReport
        {
            CpuName = Clean(snapshot.CpuName),
            PhysicalCores = Cores(snapshot.PhysicalCores),
            LogicalCores = Cores(snapshot.LogicalCores),
            GpuName = gpuName,
            GpuMemoryBytes = vram,
            TotalMemoryBytes = snapshot.TotalMemoryBytes is > 0 ? snapshot.TotalMemoryBytes : null,
            StorageType = StorageMedia.Describe(snapshot.StorageMediaTypes),
            MonitorRefreshHz = RefreshHz(snapshot.MonitorRefreshHz),
            WindowsVersion = WindowsVersion(snapshot.WindowsProductName, snapshot.WindowsDisplayVersion),
            WindowsBuild = WindowsBuild(snapshot.WindowsCurrentBuild, snapshot.WindowsUbr),
            Architecture = Clean(snapshot.Architecture),
            VirtualizationFirmwareEnabled = snapshot.VirtualizationFirmwareEnabled,
            HypervisorPresent = snapshot.HypervisorPresent,
            Warnings = snapshot.Warnings ?? []
        };
    }

    public static int? Cores(int? value) => value is > 0 and <= 512 ? value : null;

    public static int? RefreshHz(int? hz) => hz is >= 20 and <= 1000 ? hz : null;

    public static long? Vram(long? bytes) => bytes is > 0 and < RejectedVramAtOrAbove ? bytes : null;

    public static (string? Name, long? Vram) DescribeGpus(IReadOnlyList<GpuReading>? gpus)
    {
        if (gpus is null || gpus.Count == 0)
        {
            return (null, null);
        }

        var hasRealAdapter = false;
        foreach (var gpu in gpus)
        {
            var name = Clean(gpu.Name);
            if (name is not null && !IsBasicDisplay(name))
            {
                hasRealAdapter = true;
                break;
            }
        }

        var names = new List<string>();
        long? vram = null;
        foreach (var gpu in gpus)
        {
            var name = Clean(gpu.Name);
            if (name is null)
            {
                continue;
            }

            if (hasRealAdapter && IsBasicDisplay(name))
            {
                continue;
            }

            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(name);
            }

            var candidate = Vram(gpu.AdapterRamBytes);
            if (candidate is not null && (vram is null || candidate > vram))
            {
                vram = candidate;
            }
        }

        return (names.Count == 0 ? null : string.Join(", ", names), vram);
    }

    public static string? WindowsVersion(string? productName, string? displayVersion)
    {
        var product = Clean(productName);
        var display = Clean(displayVersion);
        if (product is null)
        {
            return display;
        }

        if (display is null || product.Contains(display, StringComparison.OrdinalIgnoreCase))
        {
            return product;
        }

        return product + " " + display;
    }

    public static string? WindowsBuild(string? currentBuild, string? ubr)
    {
        var build = Digits(currentBuild);
        if (build is null)
        {
            return null;
        }

        var update = Digits(ubr);
        return update is null ? build : build + "." + update;
    }

    private static bool IsBasicDisplay(string name) =>
        name.Equals("Microsoft Basic Display Adapter", StringComparison.OrdinalIgnoreCase);

    private static string? Digits(string? value)
    {
        var cleaned = Clean(value);
        if (cleaned is null)
        {
            return null;
        }

        foreach (var character in cleaned)
        {
            if (!char.IsDigit(character))
            {
                return null;
            }
        }

        return cleaned;
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? null : string.Join(' ', parts);
    }
}

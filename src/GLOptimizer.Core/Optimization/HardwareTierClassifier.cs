using GLOptimizer.Core.Models;

namespace GLOptimizer.Core.Optimization;

public static class HardwareTierClassifier
{
    public const long Gibibyte = 1024L * 1024L * 1024L;

    public static HardwareTier Classify(HardwareReport? report)
    {
        if (report?.LogicalCores is not int cores || cores <= 0)
        {
            return HardwareTier.Unknown;
        }

        if (report.TotalMemoryBytes is not long ram || ram <= 0)
        {
            return HardwareTier.Unknown;
        }

        if (cores <= 4 || ram < 8 * Gibibyte)
        {
            return HardwareTier.Low;
        }

        var smallGpu = report.GpuMemoryBytes is long vram && vram > 0 && vram < 2 * Gibibyte;
        if (cores >= 8 && ram >= 16 * Gibibyte && !smallGpu)
        {
            return HardwareTier.High;
        }

        return HardwareTier.Mid;
    }

    /// <summary>
    /// At most half of the logical processors, and never every processor.
    /// </summary>
    public static int? CpuHeadroom(int? logicalCores)
    {
        if (logicalCores is not int cores || cores < 2)
        {
            return null;
        }

        var half = cores / 2;
        return half >= 1 && half < cores ? half : null;
    }

    /// <summary>
    /// At most half of RAM, and at least 4 GB left for Windows. Rounded down to 256 MB.
    /// </summary>
    public static int? MemoryHeadroomMb(long? totalBytes)
    {
        if (totalBytes is not long ram || ram < 6 * Gibibyte)
        {
            return null;
        }

        var totalMb = ram / (1024 * 1024);
        var cap = Math.Min(totalMb / 2, totalMb - (4 * 1024));
        cap -= cap % 256;
        if (cap < 512 || cap > 131072)
        {
            return null;
        }

        return (int)cap;
    }
}

using GLOptimizer.Core.Models;

namespace GLOptimizer.Core.Diagnostics;

public sealed class FirstRunSummary
{
    public required string Hardware { get; init; }

    public required string GameLoop { get; init; }

    public required string Games { get; init; }

    public required string Virtualization { get; init; }

    public int RecommendationCount { get; init; }

    public string Text { get; init; } = string.Empty;

    public static FirstRunSummary Create(HardwareReport? hardware, GameLoopScan? scan, int recommendationCount)
    {
        var cpu = string.IsNullOrWhiteSpace(hardware?.CpuName) ? "Unknown" : hardware!.CpuName!;
        var gameLoop = scan is null
            ? "Unknown"
            : scan.Installations.Count == 0
                ? "Not found"
                : scan.Installations.Count + " verified install(s)";
        var pubg = scan?.PubgMobile.Status.ToString() ?? "Unknown";
        var cod = scan?.CodMobile.Status.ToString() ?? "Unknown";
        var virtualization = DescribeVirtualization(hardware);
        var text = "Hardware: " + cpu
            + "\nGameLoop: " + gameLoop
            + "\nPUBG Mobile: " + pubg
            + "\nCOD Mobile: " + cod
            + "\nVirtualization: " + virtualization
            + "\nRecommendations: " + recommendationCount;
        return new FirstRunSummary
        {
            Hardware = cpu,
            GameLoop = gameLoop,
            Games = "PUBG Mobile " + pubg + ". COD Mobile " + cod + ".",
            Virtualization = virtualization,
            RecommendationCount = recommendationCount,
            Text = text
        };
    }

    public static string DescribeVirtualization(HardwareReport? hardware)
    {
        if (hardware is null)
        {
            return "Unknown";
        }

        if (hardware.VirtualizationFirmwareEnabled is null && hardware.HypervisorPresent is null)
        {
            return "Unknown";
        }

        var firmware = hardware.VirtualizationFirmwareEnabled switch
        {
            true => "Firmware virtualization is enabled.",
            false => "Firmware virtualization is disabled.",
            _ => "Firmware virtualization was not reported."
        };
        var hypervisor = hardware.HypervisorPresent switch
        {
            true => " A hypervisor is present.",
            false => " A hypervisor was not reported as present.",
            _ => string.Empty
        };
        return firmware + hypervisor;
    }
}

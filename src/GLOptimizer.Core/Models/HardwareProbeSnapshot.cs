namespace GLOptimizer.Core.Models;

/// <summary>
/// Raw probe output. Sanity checks happen in <c>HardwareReportBuilder</c>, not here.
/// </summary>
public sealed class HardwareProbeSnapshot
{
    public string? CpuName { get; init; }

    public int? PhysicalCores { get; init; }

    public int? LogicalCores { get; init; }

    public IReadOnlyList<GpuReading> Gpus { get; init; } = [];

    public long? TotalMemoryBytes { get; init; }

    public IReadOnlyList<int> StorageMediaTypes { get; init; } = [];

    public int? MonitorRefreshHz { get; init; }

    public string? WindowsProductName { get; init; }

    public string? WindowsDisplayVersion { get; init; }

    public string? WindowsCurrentBuild { get; init; }

    public string? WindowsUbr { get; init; }

    public string? Architecture { get; init; }

    public bool? VirtualizationFirmwareEnabled { get; init; }

    public bool? HypervisorPresent { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];
}

public sealed class GpuReading
{
    public string? Name { get; init; }

    public long? AdapterRamBytes { get; init; }
}

using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Models;

namespace GLOptimizer.Tests;

public class HardwareReportBuilderTests
{
    [Fact]
    public void Implausible_readings_stay_empty()
    {
        var report = HardwareReportBuilder.Build(new HardwareProbeSnapshot
        {
            CpuName = "  Example   CPU  ",
            PhysicalCores = 0,
            LogicalCores = 513,
            Gpus =
            [
                new GpuReading { Name = "Microsoft Basic Display Adapter", AdapterRamBytes = 0 },
                new GpuReading { Name = "Example GPU", AdapterRamBytes = 0xFFFF0000L }
            ],
            TotalMemoryBytes = -1,
            StorageMediaTypes = [0, 5],
            MonitorRefreshHz = 1,
            WindowsProductName = "Windows 10 Pro",
            WindowsDisplayVersion = "22H2",
            WindowsCurrentBuild = "19045",
            WindowsUbr = "3803",
            Architecture = "X64"
        });

        Assert.Equal("Example CPU", report.CpuName);
        Assert.Null(report.PhysicalCores);
        Assert.Null(report.LogicalCores);
        Assert.Equal("Example GPU", report.GpuName);
        Assert.Null(report.GpuMemoryBytes);
        Assert.Null(report.TotalMemoryBytes);
        Assert.Null(report.StorageType);
        Assert.Null(report.MonitorRefreshHz);
        Assert.Equal("Windows 10 Pro 22H2", report.WindowsVersion);
        Assert.Equal("19045.3803", report.WindowsBuild);
        Assert.Equal("X64", report.Architecture);
    }

    [Fact]
    public void Valid_gpu_memory_refresh_and_storage_are_kept()
    {
        var report = HardwareReportBuilder.Build(new HardwareProbeSnapshot
        {
            PhysicalCores = 8,
            LogicalCores = 16,
            Gpus = [new GpuReading { Name = "Example GPU", AdapterRamBytes = 2L * 1024 * 1024 * 1024 }],
            TotalMemoryBytes = 16L * 1024 * 1024 * 1024,
            StorageMediaTypes = [3, 4],
            MonitorRefreshHz = 144
        });

        Assert.Equal(8, report.PhysicalCores);
        Assert.Equal(16, report.LogicalCores);
        Assert.Equal(2L * 1024 * 1024 * 1024, report.GpuMemoryBytes);
        Assert.Equal(16L * 1024 * 1024 * 1024, report.TotalMemoryBytes);
        Assert.Equal("SSD + HDD", report.StorageType);
        Assert.Equal(144, report.MonitorRefreshHz);
    }

    [Fact]
    public void Basic_display_is_kept_when_it_is_the_only_adapter()
    {
        var report = HardwareReportBuilder.Build(new HardwareProbeSnapshot
        {
            Gpus = [new GpuReading { Name = "Microsoft Basic Display Adapter", AdapterRamBytes = 0 }]
        });

        Assert.Equal("Microsoft Basic Display Adapter", report.GpuName);
        Assert.Null(report.GpuMemoryBytes);
    }

    [Theory]
    [InlineData(19)]
    [InlineData(1001)]
    [InlineData(0)]
    public void Refresh_outside_the_sane_range_is_dropped(int hz)
    {
        Assert.Null(HardwareReportBuilder.RefreshHz(hz));
    }
}

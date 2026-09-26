using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;
using GLOptimizer.GameLoop;
using GLOptimizer.Monitoring;

namespace GLOptimizer.Tests;

public class WindowsDetectionSmokeTests
{
    [Fact]
    public async Task Host_probes_do_not_throw_or_invent_windows_only_fields()
    {
        var hardware = await new HardwareDetector(new WindowsHardwareProbe()).GetReportAsync();
        var scan = await new GameLoopDetector(new WindowsGameLoopEnvironment()).DetectAsync();

        Assert.Equal(OperationStatus.Success, hardware.Status);
        Assert.Equal(OperationStatus.Success, scan.Status);
        Assert.False(string.IsNullOrWhiteSpace(hardware.Value!.Architecture));
        Assert.NotNull(scan.Value);

        if (!OperatingSystem.IsWindows())
        {
            Assert.Null(hardware.Value.CpuName);
            Assert.Null(hardware.Value.GpuName);
            Assert.Null(hardware.Value.TotalMemoryBytes);
            Assert.Null(hardware.Value.WindowsVersion);
            Assert.Null(hardware.Value.MonitorRefreshHz);
            if (scan.Value.Installations.Count == 0)
            {
                Assert.Equal(DiagnosticState.Warning, DiagnosticAssessment.Evaluate(hardware.Value, scan.Value, false, false));
                Assert.Equal(GamePresenceStatus.Unknown, scan.Value.PubgMobile.Status);
                Assert.Equal(GamePresenceStatus.Unknown, scan.Value.CodMobile.Status);
            }
        }
    }
}

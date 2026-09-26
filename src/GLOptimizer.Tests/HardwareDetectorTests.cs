using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;
using GLOptimizer.Monitoring;

namespace GLOptimizer.Tests;

public class HardwareDetectorTests
{
    [Fact]
    public async Task Probe_values_pass_through_sanity_checks()
    {
        var detector = new HardwareDetector(new FakeProbe(new HardwareProbeSnapshot
        {
            CpuName = "Example CPU",
            PhysicalCores = 4,
            LogicalCores = 8,
            TotalMemoryBytes = 1024,
            Architecture = "X64",
            MonitorRefreshHz = 60
        }));

        var result = await detector.GetReportAsync();

        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Equal("Example CPU", result.Value!.CpuName);
        Assert.Equal(4, result.Value.PhysicalCores);
        Assert.Equal(60, result.Value.MonitorRefreshHz);
        Assert.Equal("X64", result.Value.Architecture);
    }

    [Fact]
    public async Task Probe_exceptions_become_a_failure()
    {
        var result = await new HardwareDetector(new FakeProbe(null, throwOnCapture: true)).GetReportAsync();

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Null(result.Value);
        Assert.Equal("Hardware could not be read.", result.Error);
    }

    [Fact]
    public async Task Cancelled_hardware_scan_fails_without_a_value()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var result = await new HardwareDetector(new FakeProbe(new HardwareProbeSnapshot())).GetReportAsync(cts.Token);

        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Null(result.Value);
    }

    private sealed class FakeProbe : IHardwareProbe
    {
        private readonly HardwareProbeSnapshot? _snapshot;
        private readonly bool _throwOnCapture;

        public FakeProbe(HardwareProbeSnapshot? snapshot, bool throwOnCapture = false)
        {
            _snapshot = snapshot;
            _throwOnCapture = throwOnCapture;
        }

        public HardwareProbeSnapshot Capture(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_throwOnCapture)
            {
                throw new InvalidOperationException("probe failed");
            }

            return _snapshot ?? new HardwareProbeSnapshot();
        }
    }
}

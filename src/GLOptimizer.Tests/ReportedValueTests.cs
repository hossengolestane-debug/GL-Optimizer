using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Tests;

public class ReportedValueTests
{
    [Fact]
    public void Not_implemented_results_do_not_become_numbers()
    {
        Assert.Equal(ReportedValue.Unavailable, ReportedValue.HardwareCpu(OperationResult<HardwareReport>.NotImplemented("Hardware report")));
        Assert.Equal(ReportedValue.Unavailable, ReportedValue.FramesPerSecond(OperationResult<FrameSample>.NotImplemented("Frame metrics")));
        Assert.Equal(ReportedValue.Unavailable, ReportedValue.GameLoopVersion(OperationResult<GameLoopInstallation>.NotImplemented("GameLoop detection")));
        Assert.Equal(ReportedValue.Unavailable, ReportedValue.BackupCount(OperationResult<IReadOnlyList<BackupRecord>>.NotImplemented("Backup list")));
    }

    [Fact]
    public void Real_values_are_shown_and_blank_reports_stay_unavailable()
    {
        var named = OperationResult<HardwareReport>.Success(new HardwareReport { CpuName = "Example CPU" });
        var blank = OperationResult<HardwareReport>.Success(new HardwareReport());
        var frames = OperationResult<FrameSample>.Success(new FrameSample { FramesPerSecond = 59.4 });
        var rejected = OperationResult<FrameSample>.Success(new FrameSample { FramesPerSecond = double.NaN });

        Assert.Equal("Example CPU", ReportedValue.HardwareCpu(named));
        Assert.Equal(ReportedValue.Unavailable, ReportedValue.HardwareCpu(blank));
        Assert.Equal("59.4", ReportedValue.FramesPerSecond(frames));
        Assert.Equal(ReportedValue.Unavailable, ReportedValue.FramesPerSecond(rejected));
    }

    [Fact]
    public void Backup_count_uses_the_returned_list()
    {
        var result = OperationResult<IReadOnlyList<BackupRecord>>.Success(
        [
            new BackupRecord { Id = "1", Label = "Manual", CreatedAt = DateTimeOffset.UnixEpoch, SizeBytes = 10 }
        ]);

        Assert.Equal("1", ReportedValue.BackupCount(result));
    }
}

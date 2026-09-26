using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Models;

namespace GLOptimizer.Tests;

public class DiagnosticAssessmentTests
{
    [Fact]
    public void Missing_gameloop_is_a_warning()
    {
        var state = DiagnosticAssessment.Evaluate(
            new HardwareReport { CpuName = "Example CPU", TotalMemoryBytes = 1024 },
            new GameLoopScan(),
            false,
            false);

        Assert.Equal(DiagnosticState.Warning, state);
        Assert.Equal("WARNING", DetectionText.State(state));
    }

    [Fact]
    public void Broken_registration_without_an_install_requires_action()
    {
        var state = DiagnosticAssessment.Evaluate(
            new HardwareReport { CpuName = "Example CPU" },
            new GameLoopScan { BrokenRegistration = true },
            false,
            false);

        Assert.Equal(DiagnosticState.ActionRequired, state);
    }

    [Fact]
    public void Verified_install_and_cpu_are_good()
    {
        var state = DiagnosticAssessment.Evaluate(
            new HardwareReport { TotalMemoryBytes = 1024 },
            new GameLoopScan
            {
                Installations = [new GameLoopInstallation { InstallPath = "/opt/GameLoop", RunStatus = GameRunStatus.Stopped }]
            },
            false,
            false);

        Assert.Equal(DiagnosticState.Good, state);
        Assert.Equal("GOOD", DetectionText.State(state));
    }

    [Fact]
    public void Install_without_cpu_or_memory_stays_a_warning()
    {
        var state = DiagnosticAssessment.Evaluate(
            new HardwareReport { Architecture = "X64" },
            new GameLoopScan { Installations = [new GameLoopInstallation { InstallPath = "/opt/GameLoop" }] },
            false,
            false);

        Assert.Equal(DiagnosticState.Warning, state);
    }

    [Fact]
    public void Both_scans_failing_requires_action()
    {
        Assert.Equal(DiagnosticState.ActionRequired, DiagnosticAssessment.Evaluate(null, null, true, true));
    }

    [Fact]
    public void A_failed_scan_alone_is_a_warning()
    {
        Assert.Equal(DiagnosticState.Warning, DiagnosticAssessment.Evaluate(new HardwareReport { CpuName = "CPU" }, null, false, true));
    }

    [Fact]
    public void Product_badges_do_not_invent_a_mismatch()
    {
        var missing = DetectionText.ForGameLoop(new GameLoopScan(), false);
        var running = DetectionText.ForGameLoop(
            new GameLoopScan { Installations = [new GameLoopInstallation { RunStatus = GameRunStatus.Running }] },
            false);
        var unknown = DetectionText.ForMobile(new MobileGamePresence(), false);

        Assert.Equal("Not found", missing.Badge);
        Assert.Equal("Ready", running.Badge);
        Assert.Equal("Unknown", unknown.Badge);
        Assert.DoesNotContain("MISMATCH", missing.Badge + running.Badge + unknown.Badge, StringComparison.OrdinalIgnoreCase);
    }
}

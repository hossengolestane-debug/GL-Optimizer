using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Launch;
using GLOptimizer.Core.Optimization;
using GLOptimizer.Core.Recovery;
using GLOptimizer.Core.Repair;
using GLOptimizer.Infrastructure;
using GLOptimizer.Infrastructure.Launch;
using GLOptimizer.Infrastructure.Optimization;
using GLOptimizer.Infrastructure.Repair;

namespace GLOptimizer.Tests;

public class RecoveryTests
{
    [Fact]
    public void Original_path_rejects_parent_segments_and_accepts_a_path_under_one_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "glopt-recovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var full = Path.GetFullPath(root);
            Assert.Null(RepairRecovery.OriginalPath([full], @"..\outside.txt"));
            Assert.Null(RepairRecovery.OriginalPath([full], "/etc/passwd"));
            Assert.Null(RepairRecovery.OriginalPath([full], @"AppMarket\..\..\outside.txt"));
            var expected = Path.Combine(full, "AppMarket", "cache", "a.txt");
            Assert.Equal(expected, RepairRecovery.OriginalPath([full], @"AppMarket\cache\a.txt"));
            Assert.Equal(expected, RepairRecovery.OriginalPath([full], "AppMarket/cache/a.txt"));
            Assert.False(RepairRecovery.NeedsRollback(null));
            Assert.False(RepairRecovery.NeedsRollback(new RepairCheckpoint { BackupId = "id", AwaitingRefresh = true }));
            Assert.True(RepairRecovery.NeedsRollback(new RepairCheckpoint { BackupId = "id", InProgress = true }));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Startup_recovery_prefers_repair_then_optimization_then_launch()
    {
        var launch = new LaunchRecovery
        {
            JournalPresent = true,
            Message = "A previous Launch Optimized session did not finish. Recovery can restore the saved priorities."
        };
        var repair = StartupRecoveryLogic.Inspect(
            new RepairCheckpoint { BackupId = "repair", InProgress = true },
            new OptimizationUndoRecord { BackupId = "opt", InProgress = true },
            launch);
        Assert.Equal(RecoveryKind.Repair, repair.Kind);
        Assert.Equal("Roll back repair", repair.ActionLabel);

        var optimization = StartupRecoveryLogic.Inspect(
            new RepairCheckpoint { BackupId = "repair", AwaitingRefresh = true },
            new OptimizationUndoRecord { BackupId = "opt", InProgress = true },
            launch);
        Assert.Equal(RecoveryKind.Optimization, optimization.Kind);
        Assert.Equal("Restore backup", optimization.ActionLabel);

        var journal = StartupRecoveryLogic.Inspect(null, new OptimizationUndoRecord(), launch);
        Assert.Equal(RecoveryKind.Launch, journal.Kind);
        Assert.Equal("Restore priorities", journal.ActionLabel);

        Assert.Equal(RecoveryKind.None, StartupRecoveryLogic.Inspect(null, null, new LaunchRecovery()).Kind);
    }

    [Fact]
    public void Corrupt_checkpoint_journal_and_optimization_record_do_not_throw()
    {
        var parent = Path.Combine(Path.GetTempPath(), "glopt-corrupt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        try
        {
            var locations = new AppDataLocations(parent);
            Directory.CreateDirectory(locations.Root);
            File.WriteAllText(Path.Combine(locations.Root, "repair-checkpoint.json"), "{");
            File.WriteAllText(Path.Combine(locations.Root, "launch-optimized.json"), "{");
            File.WriteAllText(Path.Combine(locations.Root, "last-optimization.json"), "{");

            var repair = new JsonRepairStateStore(locations);
            Assert.Null(repair.Load());
            Assert.Contains("could not be read", repair.LastProblem, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(Path.Combine(locations.Root, "repair-checkpoint.json")));
            repair.Clear();
            Assert.False(File.Exists(Path.Combine(locations.Root, "repair-checkpoint.json")));

            var journal = new JsonLaunchJournalStore(locations);
            Assert.Null(journal.Load());
            Assert.Contains("could not be read", journal.LastProblem, StringComparison.OrdinalIgnoreCase);

            var record = new JsonOptimizationRecordStore(locations);
            var read = record.Read();
            Assert.False(read.Succeeded);
            Assert.Contains("could not be read", record.LastProblem, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }
}

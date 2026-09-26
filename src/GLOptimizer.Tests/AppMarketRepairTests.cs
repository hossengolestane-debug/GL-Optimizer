using System.Text.Json;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Backup;
using GLOptimizer.Core.Configuration;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Repair;
using GLOptimizer.Core.Results;
using GLOptimizer.Core.Settings;
using GLOptimizer.GameLoop;
using GLOptimizer.Infrastructure;
using GLOptimizer.Infrastructure.Backup;
using GLOptimizer.Infrastructure.Repair;

namespace GLOptimizer.Tests;

public class AppMarketRepairTests
{
    [Fact]
    public void Eligibility_moves_only_cache_and_never_metadata_packages_or_unknown()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "glopt-rules"));
        var assessment = AppMarketRepairRules.Assess(
        [
            Cache(root, "AppMarket/cache/a.txt", 12, 1),
            Item(root, "AppMarket/meta.json", MarketItemKind.Metadata, 4, 1),
            Item(root, "AppMarket/com.activision.callofduty.shooter", MarketItemKind.Package, 8, 1, directory: true),
            Item(root, "notes.bin", MarketItemKind.Unknown, 3, 1),
            Item(root, "AppMarket/game.apk", MarketItemKind.Package, 9, 1)
        ]);

        Assert.Single(assessment.Clear);
        Assert.Equal("AppMarket/cache/a.txt", assessment.Clear[0].RelativePath);
        Assert.Single(assessment.Backup);
        Assert.Equal("AppMarket/meta.json", assessment.Backup[0].RelativePath);
        Assert.Empty(assessment.Unexpected);
        Assert.True(assessment.CanRepair);
        Assert.DoesNotContain(assessment.Clear, item => item.Kind != MarketItemKind.Cache);
        Assert.DoesNotContain(assessment.Backup, item => item.Kind != MarketItemKind.Metadata);
    }

    [Fact]
    public void Guard_limits_and_protected_paths_need_review()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "glopt-guard"));
        var tooMany = AppMarketRepairRules.Assess([Cache(root, "AppMarket/cache/a.txt", 1, AppMarketRepairRules.MaxFiles + 1)]);
        Assert.True(tooMany.NeedsReview);
        Assert.Contains("20000", tooMany.ReviewReason, StringComparison.Ordinal);
        Assert.False(tooMany.CanRepair);

        var tooLarge = AppMarketRepairRules.Assess([Cache(root, "AppMarket/cache/a.txt", AppMarketRepairRules.MaxBytes + 1, 1)]);
        Assert.True(tooLarge.NeedsReview);
        Assert.Contains("5 GB", tooLarge.ReviewReason, StringComparison.Ordinal);

        var protectedFile = AppMarketRepairRules.Assess(
        [
            Cache(root, "AppMarket/cache/a.txt", 1, 1),
            Cache(root, "AppMarket/cache/game.apk", 1, 1)
        ]);
        Assert.True(protectedFile.NeedsReview);
        Assert.Contains("not safe", protectedFile.ReviewReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(protectedFile.Unexpected, path => path.EndsWith("game.apk", StringComparison.Ordinal));

        var keymap = AppMarketRepairRules.Assess([Cache(root, "AppMarket/cache/keymap.json", 1, 1)]);
        Assert.Contains(keymap.Unexpected, path => path.EndsWith("keymap.json", StringComparison.Ordinal));

        var packageDir = AppMarketRepairRules.Assess([Cache(root, "com.tencent.ig", 1, 1, directory: true)]);
        Assert.Contains("com.tencent.ig", packageDir.Unexpected);

        var reparse = AppMarketRepairRules.Assess([Cache(root, "AppMarket/cache", 1, 1, reparse: true)]);
        Assert.True(reparse.NeedsReview);
        Assert.False(reparse.CanRepair);
    }

    [Fact]
    public void Dry_run_text_lists_exactly_what_the_repair_would_do()
    {
        var text = new RepairScript
        {
            StopPaths = ["GameLoop C:\\GameLoop\\GameLoop.exe"],
            SessionWarning = ProcessSelection.SessionWarning,
            BackupPaths = ["C:\\GameLoop\\AppMarket\\meta.json"],
            ClearPaths = ["C:\\GameLoop\\AppMarket\\cache\\a.txt"]
        }.Format();

        var expected = string.Join(
            Environment.NewLine,
            [
                "Would stop",
                "- GameLoop C:\\GameLoop\\GameLoop.exe",
                ProcessSelection.SessionWarning,
                "Would back up",
                "- C:\\GameLoop\\AppMarket\\meta.json",
                "Would clear",
                "- C:\\GameLoop\\AppMarket\\cache\\a.txt",
                "Would restart",
                "Start GameLoop is offered after the repair. It is not started automatically.",
                "No game data will be removed.",
                string.Empty
            ]);
        Assert.Equal(expected, text);

        var review = new RepairScript
        {
            NeedsReview = true,
            ReviewReason = "The cache set is larger than 5 GB."
        }.Format();
        Assert.Contains("Would stop", review, StringComparison.Ordinal);
        Assert.Contains("- None. GameLoop is not running.", review, StringComparison.Ordinal);
        Assert.Contains("Would back up", review, StringComparison.Ordinal);
        Assert.Contains("- No metadata files were selected.", review, StringComparison.Ordinal);
        Assert.Contains("- No cache files were selected.", review, StringComparison.Ordinal);
        Assert.Contains("Would restart", review, StringComparison.Ordinal);
        Assert.Contains("No game data will be removed.", review, StringComparison.Ordinal);
        Assert.Contains("Needs review: The cache set is larger than 5 GB.", review, StringComparison.Ordinal);
    }

    [Fact]
    public void Verdict_matrix_distinguishes_local_refresh_from_a_remote_catalog()
    {
        var failed = RepairVerdictLogic.Evaluate(false, true, "1.0.0", "1.0.0", "2.0.0");
        Assert.Equal(RepairVerdictKind.Failed, failed.Kind);
        Assert.Equal(RepairVerdictLogic.FailedMessage, failed.Message);
        Assert.NotEqual(RepairVerdictKind.RemoteCatalogIssue, failed.Kind);

        var waiting = RepairVerdictLogic.Evaluate(true, false, "1.0.0", "2.0.0", "2.0.0");
        Assert.Equal(RepairVerdictKind.AwaitingRefresh, waiting.Kind);
        Assert.Equal(RepairVerdictLogic.AwaitingMessage, waiting.Message);

        var advanced = RepairVerdictLogic.Evaluate(true, true, "1.0.0", "2.0.0", "2.0.0");
        Assert.Equal(RepairVerdictKind.LocalRefreshed, advanced.Kind);
        Assert.Equal(RepairVerdictLogic.LocalMessage("2.0.0"), advanced.Message);

        var matched = RepairVerdictLogic.Evaluate(true, true, "2.0.0", "2.0.0", "2.0.0");
        Assert.Equal(RepairVerdictKind.LocalRefreshed, matched.Kind);
        Assert.Equal("Local App Market refreshed. COD Mobile market version is now 2.0.0.", matched.Message);

        var remote = RepairVerdictLogic.Evaluate(true, true, "1.0.0", "1.0.0", "2.0.0");
        Assert.Equal(RepairVerdictKind.RemoteCatalogIssue, remote.Kind);
        Assert.Equal(CatalogComparison.RemoteCatalogIssue, remote.Comparison);
        Assert.Equal(
            RepairVerdictLogic.RemoteBody + " " + CatalogComparisonLogic.RemoteMessage,
            remote.Message);
        Assert.Equal(
            "The local App Market has been refreshed successfully. However, the currently detected GameLoop server catalog still provides the same COD Mobile version. GL Optimizer cannot safely change GameLoop's remote catalog.",
            RepairVerdictLogic.RemoteBody);
        Assert.Equal(
            "Server-side GameLoop catalog issue detected. This cannot safely be modified locally.",
            CatalogComparisonLogic.RemoteMessage);

        var missingInstalled = RepairVerdictLogic.Evaluate(true, true, "1.0.0", "1.0.0", null);
        Assert.Equal(RepairVerdictKind.RemoteCatalogIssue, missingInstalled.Kind);

        var ambiguous = RepairVerdictLogic.Evaluate(true, true, "1.0.0", "beta", "2.0.0");
        Assert.Equal(RepairVerdictKind.Unknown, ambiguous.Kind);
        Assert.Contains("not unambiguous", ambiguous.Message, StringComparison.Ordinal);

        var movedSideways = RepairVerdictLogic.Evaluate(true, true, "2.0.0", "1.5.0", "3.0.0");
        Assert.Equal(RepairVerdictKind.Unknown, movedSideways.Kind);
        Assert.Equal(CatalogComparisonLogic.CannotDistinguish, movedSideways.Message);

        Assert.NotEqual(
            CatalogComparison.RemoteCatalogIssue,
            CatalogComparisonLogic.Evaluate("2.0.0", "1.0.0", "2.0.0").Comparison);
    }

    [Fact]
    public void Process_selection_keeps_only_gameloop_executables_inside_the_install()
    {
        var root = Path.Combine(Path.GetTempPath(), "glopt-proc-" + Guid.NewGuid().ToString("N"));
        var outside = Path.Combine(Path.GetTempPath(), "glopt-proc-out-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        try
        {
            var processes = new[]
            {
                Process(1, "GameLoop", Path.Combine(root, "GameLoop.exe")),
                Process(2, "aow_exe", Path.Combine(root, "aow_exe.exe")),
                Process(3, "GameLoop", Path.Combine(outside, "GameLoop.exe")),
                Process(4, "notepad", Path.Combine(outside, "notepad.exe")),
                Process(5, "AppMarket", null),
                Process(6, "codm", Path.Combine(root, "codm.exe"))
            };

            var selected = ProcessSelection.SelectStopTargets(processes, [root]);
            Assert.Equal([1, 2], selected.Select(item => item.ProcessId).OrderBy(id => id).ToArray());
            Assert.True(ProcessSelection.GameSessionAppearsActive(processes, [], [root]));
            Assert.True(ProcessSelection.GameSessionAppearsActive([], ["PUBG Mobile"], [root]));
            Assert.False(ProcessSelection.GameSessionAppearsActive([Process(4, "notepad", Path.Combine(outside, "notepad.exe"))], ["Notepad"], [root]));
        }
        finally
        {
            Directory.Delete(root, true);
            Directory.Delete(outside, true);
        }
    }

    [Fact]
    public async Task Stopper_force_terminates_only_a_process_still_inside_the_install()
    {
        var root = Path.Combine(Path.GetTempPath(), "glopt-stop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var outside = Path.Combine(Path.GetTempPath(), "outside-" + Guid.NewGuid().ToString("N") + ".exe");
        try
        {
            var processes = new FakeProcesses();
            processes.Items.Add(Process(7, "GameLoop", Path.Combine(root, "GameLoop.exe")));
            processes.Items.Add(Process(8, "notepad", outside));
            var stopper = new GameLoopSessionStopper(processes, new NoDelay(), new FixedClock(), TimeSpan.Zero);

            var asked = await stopper.StopAsync([root], forceConfirmed: false, CancellationToken.None);
            Assert.True(asked.NeedsForceConfirmation);
            Assert.False(asked.Stopped);
            Assert.Equal(ProcessStopMessages.ForceRequired, asked.Message);
            Assert.Empty(processes.Terminated);
            Assert.Equal([7], processes.Closed);

            var forced = await stopper.StopAsync([root], forceConfirmed: true, CancellationToken.None);
            Assert.True(forced.Stopped);
            Assert.Equal([7], processes.Terminated);
            Assert.DoesNotContain(8, processes.Terminated);

            processes.Items.Add(Process(7, "GameLoop", Path.Combine(root, "GameLoop.exe")));
            processes.Terminated.Clear();
            processes.ResetCounting();
            processes.LeaveAfterLists = 4;
            processes.OutsidePath = outside;
            var slipped = await stopper.StopAsync([root], forceConfirmed: true, CancellationToken.None);
            Assert.True(slipped.Stopped);
            Assert.Empty(processes.Terminated);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Quarantine_refuses_a_reparse_point_before_moving_anything()
    {
        using var temp = new TempTree();
        var root = temp.Dir("install");
        var real = Path.Combine(root, "real.txt");
        File.WriteAllText(real, "keep");
        var link = Path.Combine(root, "link.txt");
        File.CreateSymbolicLink(link, real);
        var backup = temp.Dir("backup");

        var moved = QuarantineStore.Move(new QuarantineMoveRequest
        {
            BackupDirectory = backup,
            InstallRoot = root,
            SourceFiles = [link]
        }, CancellationToken.None);

        Assert.False(moved.Succeeded);
        Assert.Contains("re-validation", moved.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("keep", File.ReadAllText(real));
        Assert.False(Directory.Exists(Path.Combine(backup, QuarantineStore.FolderName)));
    }

    [Fact]
    public void Quarantine_rolls_back_when_a_later_move_fails()
    {
        using var temp = new TempTree();
        var root = temp.Dir("install");
        var first = Path.Combine(root, "a.txt");
        var second = Path.Combine(root, "b.txt");
        File.WriteAllText(first, "alpha");
        File.WriteAllText(second, "bravo");
        var backup = temp.Dir("backup");

        var moved = QuarantineStore.Move(new QuarantineMoveRequest
        {
            BackupDirectory = backup,
            InstallRoot = root,
            SourceFiles = [first, second],
            BeforeFile = (index, _) =>
            {
                if (index == 1)
                {
                    throw new IOException("disk full");
                }
            }
        }, CancellationToken.None);

        Assert.False(moved.Succeeded);
        Assert.True(moved.RolledBack);
        Assert.Contains("put back", moved.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("alpha", File.ReadAllText(first));
        Assert.Equal("bravo", File.ReadAllText(second));
        Assert.Empty(moved.Moved);
    }

    [Fact]
    public void Quarantine_cancel_before_the_first_move_leaves_the_source()
    {
        using var temp = new TempTree();
        var root = temp.Dir("install");
        var file = Path.Combine(root, "a.txt");
        File.WriteAllText(file, "alpha");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var moved = QuarantineStore.Move(new QuarantineMoveRequest
        {
            BackupDirectory = temp.Dir("backup"),
            InstallRoot = root,
            SourceFiles = [file]
        }, cancellation.Token);

        Assert.False(moved.Succeeded);
        Assert.Contains("cancelled", moved.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("alpha", File.ReadAllText(file));
    }

    [Fact]
    public void Quarantine_cancel_after_a_move_rolls_back()
    {
        using var temp = new TempTree();
        var root = temp.Dir("install");
        var first = Path.Combine(root, "a.txt");
        var second = Path.Combine(root, "b.txt");
        File.WriteAllText(first, "alpha");
        File.WriteAllText(second, "bravo");

        var moved = QuarantineStore.Move(new QuarantineMoveRequest
        {
            BackupDirectory = temp.Dir("backup"),
            InstallRoot = root,
            SourceFiles = [first, second],
            BeforeFile = (index, _) =>
            {
                if (index == 1)
                {
                    throw new OperationCanceledException();
                }
            }
        }, CancellationToken.None);

        Assert.False(moved.Succeeded);
        Assert.True(moved.RolledBack);
        Assert.Equal("alpha", File.ReadAllText(first));
        Assert.Equal("bravo", File.ReadAllText(second));
    }

    [Fact]
    public void Cross_volume_copy_verifies_the_hash_before_deleting_the_source()
    {
        using var temp = new TempTree();
        var root = temp.Dir("install");
        var file = Path.Combine(root, "a.txt");
        File.WriteAllText(file, "alpha");
        var backup = temp.Dir("backup");
        var expected = QuarantineStore.HashFile(file);

        var moved = QuarantineStore.Move(new QuarantineMoveRequest
        {
            BackupDirectory = backup,
            InstallRoot = root,
            SourceFiles = [file],
            SameVolume = false
        }, CancellationToken.None);

        Assert.True(moved.Succeeded, moved.Error);
        Assert.False(File.Exists(file));
        var stored = Path.Combine(backup, QuarantineStore.FolderName, "a.txt");
        Assert.Equal(expected, QuarantineStore.HashFile(stored));
        Assert.Equal(expected, moved.Moved[0].Sha256);
        Assert.Equal(file, Path.GetFullPath(moved.Moved[0].OriginalPath));

        File.WriteAllText(file, "alpha");
        var corrupt = QuarantineStore.Move(new QuarantineMoveRequest
        {
            BackupDirectory = temp.Dir("backup2"),
            InstallRoot = root,
            SourceFiles = [file],
            SameVolume = false,
            Transfer = new CorruptCopy()
        }, CancellationToken.None);

        Assert.False(corrupt.Succeeded);
        Assert.Contains("hash", corrupt.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("alpha", File.ReadAllText(file));
        Assert.False(File.Exists(Path.Combine(temp.Root, "backup2", QuarantineStore.FolderName, "a.txt")));
    }

    [Fact]
    public async Task Service_dry_run_lists_the_repair_and_changes_nothing()
    {
        using var temp = new TempTree();
        var layout = Layout(temp);
        var processes = new FakeProcesses();
        processes.Items.Add(Process(3, "GameLoop", Path.Combine(layout.Root, "GameLoop.exe")));
        var harness = CreateHarness(temp, layout, processes, new WindowList("Call of Duty"));

        var dry = await harness.Service.DryRunAsync();

        Assert.True(dry.Succeeded, dry.Error);
        var text = dry.Value!.Text;
        Assert.Contains("Would stop", text, StringComparison.Ordinal);
        Assert.Contains("GameLoop", text, StringComparison.Ordinal);
        Assert.Contains(ProcessSelection.SessionWarning, text, StringComparison.Ordinal);
        Assert.Contains("Would back up", text, StringComparison.Ordinal);
        Assert.Contains("Would clear", text, StringComparison.Ordinal);
        Assert.Contains("Would restart", text, StringComparison.Ordinal);
        Assert.Contains("No game data will be removed.", text, StringComparison.Ordinal);
        Assert.Contains("Start GameLoop is offered after the repair. It is not started automatically.", text, StringComparison.Ordinal);
        var clear = text[(text.IndexOf("Would clear", StringComparison.Ordinal))..(text.IndexOf("Would restart", StringComparison.Ordinal))];
        Assert.Contains("a.txt", clear, StringComparison.Ordinal);
        Assert.DoesNotContain("meta.json", clear, StringComparison.Ordinal);
        Assert.Contains("meta.json", text[..text.IndexOf("Would clear", StringComparison.Ordinal)], StringComparison.Ordinal);
        Assert.True(dry.Value.CanRepair);
        Assert.Equal("cache-bytes", await File.ReadAllTextAsync(layout.CacheFile));
        Assert.False(Directory.Exists(temp.Backups));
        Assert.Empty(harness.Log.Lines);
    }

    [Fact]
    public async Task Service_moves_only_cache_and_toasts_only_when_the_repair_finishes()
    {
        using var temp = new TempTree();
        var layout = Layout(temp);
        var harness = CreateHarness(temp, layout, new FakeProcesses());

        var denied = await harness.Service.RepairAsync(confirmed: false, forceConfirmed: false);
        Assert.False(denied.Succeeded);
        Assert.Equal("cache-bytes", await File.ReadAllTextAsync(layout.CacheFile));
        Assert.DoesNotContain(harness.Log.Lines, line => line.Contains(AppMarketRepairService.CompletedToast, StringComparison.Ordinal));

        var repaired = await harness.Service.RepairAsync(confirmed: true, forceConfirmed: false);
        Assert.True(repaired.Succeeded, repaired.Error);
        Assert.True(repaired.Value!.Completed);
        Assert.True(repaired.Value.AwaitingRefresh);
        Assert.Equal(AppMarketRepairService.CompletedToast, repaired.Value.Toast);
        Assert.Equal(RepairVerdictKind.AwaitingRefresh, repaired.Value.Verdict);
        Assert.Contains(RepairVerdictLogic.AwaitingMessage, repaired.Value.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(layout.CacheFile));
        Assert.Equal("{\"v\":1}", await File.ReadAllTextAsync(layout.MetadataFile));
        Assert.Equal("apk", await File.ReadAllTextAsync(layout.ApkFile));
        Assert.Equal("unknown", await File.ReadAllTextAsync(layout.UnknownFile));
        Assert.Equal("package", await File.ReadAllTextAsync(layout.PackageFile));

        var manifestPath = Path.Combine(temp.Backups, repaired.Value.BackupId!, "manifest.json");
        var manifest = JsonSerializer.Deserialize<BackupManifest>(await File.ReadAllTextAsync(manifestPath))!;
        Assert.Equal("App Market repair", manifest.Reason);
        var quarantined = Assert.Single(manifest.Quarantine);
        Assert.Equal(Path.GetFullPath(layout.CacheFile), Path.GetFullPath(quarantined.OriginalPath));
        Assert.Equal(new FileInfo(Path.Combine(temp.Backups, manifest.Id, QuarantineStore.FolderName, quarantined.StoredRelativePath.Replace('/', Path.DirectorySeparatorChar))).Length, quarantined.SizeBytes);
        Assert.Equal(QuarantineStore.HashFile(Path.Combine(temp.Backups, manifest.Id, QuarantineStore.FolderName, quarantined.StoredRelativePath.Replace('/', Path.DirectorySeparatorChar))), quarantined.Sha256);
        Assert.Contains(manifest.MetadataCopies, copy => copy.OriginalPath.EndsWith("meta.json", StringComparison.Ordinal));
        Assert.Contains(harness.Log.Lines, line => line == "App Market " + AppMarketRepairService.CompletedToast);

        var checkpoint = new JsonRepairStateStore(new AppDataLocations(temp.AppData)).Load();
        Assert.Equal("1.0.0", checkpoint!.PreMarketVersion);
        Assert.Equal("2.0.0", checkpoint.InstalledVersion);
        Assert.True(checkpoint.AwaitingRefresh);
    }

    [Fact]
    public async Task Service_stops_when_cache_contains_a_protected_file_or_a_reparse_point()
    {
        using var temp = new TempTree();
        var layout = Layout(temp);
        var apkInCache = Path.Combine(layout.CacheDirectory, "game.apk");
        await File.WriteAllTextAsync(apkInCache, "apk-in-cache");
        var harness = CreateHarness(temp, layout, new FakeProcesses());

        var blocked = await harness.Service.RepairAsync(confirmed: true, forceConfirmed: false);
        Assert.False(blocked.Succeeded);
        Assert.Contains("not safe", blocked.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("cache-bytes", await File.ReadAllTextAsync(layout.CacheFile));
        Assert.Equal("apk-in-cache", await File.ReadAllTextAsync(apkInCache));
        Assert.DoesNotContain(harness.Log.Lines, line => line.Contains(AppMarketRepairService.CompletedToast, StringComparison.Ordinal));

        File.Delete(apkInCache);
        var real = temp.Dir("real-cache");
        var kept = Path.Combine(real, "kept.txt");
        await File.WriteAllTextAsync(kept, "keep");
        Directory.Delete(layout.CacheDirectory, recursive: true);
        Directory.CreateSymbolicLink(layout.CacheDirectory, real);
        layout.Market.Report = Report(layout, layout.CacheDirectory);

        var linked = await harness.Service.RepairAsync(confirmed: true, forceConfirmed: false);
        Assert.False(linked.Succeeded);
        Assert.Contains("not safe", linked.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("keep", await File.ReadAllTextAsync(kept));
        Assert.False(Directory.Exists(temp.Backups));
    }

    [Fact]
    public async Task Service_force_stop_is_a_second_call_and_does_not_touch_other_processes()
    {
        using var temp = new TempTree();
        var layout = Layout(temp);
        var processes = new FakeProcesses();
        processes.Items.Add(Process(4, "GameLoop", Path.Combine(layout.Root, "GameLoop.exe")));
        processes.Items.Add(Process(9, "notepad", Path.Combine(temp.Root, "notepad.exe")));
        var harness = CreateHarness(temp, layout, processes);

        var first = await harness.Service.RepairAsync(confirmed: true, forceConfirmed: false);
        Assert.True(first.Succeeded, first.Error);
        Assert.True(first.Value!.NeedsForceConfirmation);
        Assert.False(first.Value.Completed);
        Assert.Null(first.Value.Toast);
        Assert.Equal("cache-bytes", await File.ReadAllTextAsync(layout.CacheFile));
        Assert.Empty(processes.Terminated);
        Assert.False(Directory.Exists(temp.Backups));

        var second = await harness.Service.RepairAsync(confirmed: true, forceConfirmed: true);
        Assert.True(second.Succeeded, second.Error);
        Assert.True(second.Value!.Completed);
        Assert.Equal([4], processes.Terminated);
        Assert.Contains(processes.Items, process => process.ProcessId == 9);
        Assert.False(File.Exists(layout.CacheFile));
    }

    [Fact]
    public async Task Recheck_uses_the_saved_pre_repair_version_for_the_local_or_remote_verdict()
    {
        using var temp = new TempTree();
        var layout = Layout(temp);
        var locations = new AppDataLocations(temp.AppData);
        var harness = CreateHarness(temp, layout, new FakeProcesses(), locations: locations);

        var repaired = await harness.Service.RepairAsync(confirmed: true, forceConfirmed: false);
        Assert.True(repaired.Value!.Completed);
        layout.Market.Report = Report(layout, layout.CacheDirectory, market: "1.0.0", installed: "2.0.0");
        var restarted = new AppMarketRepairService(
            layout.Market,
            new FakeProcesses(),
            new WindowList(),
            new GameLoopSessionStopper(new FakeProcesses(), new NoDelay(), new FixedClock(), TimeSpan.Zero),
            new JsonRepairStateStore(locations),
            harness.Log,
            new FixedClock(),
            locations);

        var remote = await restarted.RecheckAsync();
        Assert.True(remote.Succeeded, remote.Error);
        Assert.Equal(RepairVerdictKind.RemoteCatalogIssue, remote.Value!.Verdict);
        Assert.False(remote.Value.Completed);
        Assert.Null(remote.Value.Toast);
        Assert.Equal(
            "The local App Market has been refreshed successfully. However, the currently detected GameLoop server catalog still provides the same COD Mobile version. GL Optimizer cannot safely change GameLoop's remote catalog. Server-side GameLoop catalog issue detected. This cannot safely be modified locally.",
            remote.Value.Message);
        Assert.False(new JsonRepairStateStore(locations).Load()!.AwaitingRefresh);

        var again = await restarted.RecheckAsync();
        Assert.False(again.Succeeded);
        Assert.Contains("waiting", again.Error, StringComparison.OrdinalIgnoreCase);

        using var localTemp = new TempTree();
        var localLayout = Layout(localTemp);
        var localLocations = new AppDataLocations(localTemp.AppData);
        var localHarness = CreateHarness(localTemp, localLayout, new FakeProcesses(), locations: localLocations);
        Assert.True((await localHarness.Service.RepairAsync(true, false)).Value!.Completed);
        localLayout.Market.Report = Report(localLayout, localLayout.CacheDirectory, market: "2.0.0", installed: "2.0.0");
        var localRestarted = new AppMarketRepairService(
            localLayout.Market,
            new FakeProcesses(),
            new WindowList(),
            new GameLoopSessionStopper(new FakeProcesses(), new NoDelay(), new FixedClock(), TimeSpan.Zero),
            new JsonRepairStateStore(localLocations),
            localHarness.Log,
            new FixedClock(),
            localLocations);
        var local = await localRestarted.RecheckAsync();
        Assert.Equal(RepairVerdictKind.LocalRefreshed, local.Value!.Verdict);
        Assert.Equal("Local App Market refreshed. COD Mobile market version is now 2.0.0.", local.Value.Message);
        Assert.Null(local.Value.Toast);
    }

    [Fact]
    public async Task Restore_puts_quarantined_cache_back_and_refuses_while_gameloop_runs()
    {
        using var temp = new TempTree();
        var layout = Layout(temp);
        var harness = CreateHarness(temp, layout, new FakeProcesses());
        var repaired = await harness.Service.RepairAsync(confirmed: true, forceConfirmed: false);
        Assert.False(File.Exists(layout.CacheFile));
        var source = new FixedSource(Snapshot(layout.Root));
        var backups = new FileBackupService(source, new FixedClock(), harness.Log, new AppDataLocations(temp.AppData));
        var id = repaired.Value!.BackupId!;
        var stored = QuarantinePath(temp, id);

        source.Snapshot = Snapshot(layout.Root, running: true);
        var blocked = await backups.RestoreAsync(id, confirmed: true);
        Assert.False(blocked.Succeeded);
        Assert.Contains("running", blocked.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(layout.CacheFile));
        Assert.True(File.Exists(stored));

        source.Snapshot = Snapshot(layout.Root);
        var restored = await backups.RestoreAsync(id, confirmed: true);
        Assert.True(restored.Succeeded, restored.Error);
        Assert.Equal("cache-bytes", await File.ReadAllTextAsync(layout.CacheFile));
        Assert.True(File.Exists(stored));
    }

    [Fact]
    public async Task Restore_refuses_a_bad_quarantine_hash_and_a_traversal_path()
    {
        using var temp = new TempTree();
        var layout = Layout(temp);
        var harness = CreateHarness(temp, layout, new FakeProcesses());
        var repaired = await harness.Service.RepairAsync(confirmed: true, forceConfirmed: false);
        var id = repaired.Value!.BackupId!;
        var backups = new FileBackupService(new FixedSource(Snapshot(layout.Root)), new FixedClock(), harness.Log, new AppDataLocations(temp.AppData));
        var stored = QuarantinePath(temp, id);
        var bytes = await File.ReadAllBytesAsync(stored);
        bytes[0] ^= 0xFF;
        await File.WriteAllBytesAsync(stored, bytes);

        var tampered = await backups.PreviewRestoreAsync(id);
        Assert.False(tampered.Value!.CanRestore);
        Assert.False((await backups.RestoreAsync(id, true)).Succeeded);
        Assert.False(File.Exists(layout.CacheFile));

        bytes[0] ^= 0xFF;
        await File.WriteAllBytesAsync(stored, bytes);
        var manifestPath = Path.Combine(temp.Backups, id, "manifest.json");
        var manifest = JsonSerializer.Deserialize<BackupManifest>(await File.ReadAllTextAsync(manifestPath))!;
        manifest.Quarantine[0].StoredRelativePath = "../escape.txt";
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));

        var traversal = await backups.PreviewRestoreAsync(id);
        Assert.False(traversal.Succeeded);
        Assert.False((await backups.RestoreAsync(id, true)).Succeeded);
        Assert.False(File.Exists(Path.Combine(temp.Backups, "escape.txt")));
        Assert.False(File.Exists(layout.CacheFile));
    }

    private static string QuarantinePath(TempTree temp, string id)
    {
        var manifest = JsonSerializer.Deserialize<BackupManifest>(File.ReadAllText(Path.Combine(temp.Backups, id, "manifest.json")))!;
        var relative = manifest.Quarantine[0].StoredRelativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(temp.Backups, id, QuarantineStore.FolderName, relative);
    }

    private static RepairCandidate Cache(string root, string relative, long bytes, int files, bool directory = false, bool reparse = false) =>
        Item(root, relative, MarketItemKind.Cache, bytes, files, directory, reparse);

    private static RepairCandidate Item(string root, string relative, MarketItemKind kind, long bytes, int files, bool directory = false, bool reparse = false) =>
        new()
        {
            FullPath = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)),
            RelativePath = relative,
            Kind = kind,
            IsDirectory = directory,
            SizeBytes = bytes,
            FileCount = files,
            IsReparse = reparse,
            UnderVerifiedRoot = true
        };

    private static ControlledProcess Process(int id, string name, string? path) => new()
    {
        ProcessId = id,
        ProcessName = name,
        ExecutablePath = path
    };

    private static LayoutPaths Layout(TempTree temp)
    {
        var root = temp.Dir("install");
        var cacheDirectory = Path.Combine(root, "AppMarket", "cache");
        Directory.CreateDirectory(cacheDirectory);
        var cacheFile = Path.Combine(cacheDirectory, "a.txt");
        File.WriteAllText(cacheFile, "cache-bytes");
        var metadata = Path.Combine(root, "AppMarket", "meta.json");
        File.WriteAllText(metadata, "{\"v\":1}");
        var packageDirectory = Path.Combine(root, "AppMarket", "com.activision.callofduty.shooter");
        Directory.CreateDirectory(packageDirectory);
        var packageFile = Path.Combine(packageDirectory, "version.txt");
        File.WriteAllText(packageFile, "package");
        var apk = Path.Combine(root, "AppMarket", "game.apk");
        File.WriteAllText(apk, "apk");
        var unknown = Path.Combine(root, "notes.bin");
        File.WriteAllText(unknown, "unknown");
        var layout = new LayoutPaths(root, cacheDirectory, cacheFile, metadata, packageFile, apk, unknown, new ScriptedMarket());
        layout.Market.Report = Report(layout, cacheDirectory);
        return layout;
    }

    private static AppMarketReport Report(LayoutPaths layout, string cachePath, string market = "1.0.0", string installed = "2.0.0") => new()
    {
        StatusText = "LOCAL MARKET OUTDATED",
        InstalledVersion = installed,
        MarketVersion = market,
        InstallPaths = [Path.GetFullPath(layout.Root)],
        Inventory =
        [
            Entry(cachePath, layout.Root, MarketItemKind.Cache, directory: true),
            Entry(layout.MetadataFile, layout.Root, MarketItemKind.Metadata),
            Entry(Path.GetDirectoryName(layout.PackageFile)!, layout.Root, MarketItemKind.Package, directory: true),
            Entry(layout.ApkFile, layout.Root, MarketItemKind.Package),
            Entry(layout.UnknownFile, layout.Root, MarketItemKind.Unknown)
        ]
    };

    private static MarketInventoryItem Entry(string path, string root, MarketItemKind kind, bool directory = false) => new()
    {
        Path = Path.GetFullPath(path),
        RelativePath = Path.GetRelativePath(root, path),
        IsDirectory = directory,
        Kind = kind,
        Confidence = MarketConfidence.High,
        Reason = "fixture",
        SizeBytes = directory ? 0 : new FileInfo(path).Length,
        FileCount = directory ? 1 : 1
    };

    private static Harness CreateHarness(TempTree temp, LayoutPaths layout, FakeProcesses processes, WindowList? windows = null, AppDataLocations? locations = null)
    {
        locations ??= new AppDataLocations(temp.AppData);
        var log = new RecordingLog();
        var clock = new FixedClock();
        var service = new AppMarketRepairService(
            layout.Market,
            processes,
            windows ?? new WindowList(),
            new GameLoopSessionStopper(processes, new NoDelay(), clock, TimeSpan.Zero),
            new JsonRepairStateStore(locations),
            log,
            clock,
            locations);
        return new Harness(service, log);
    }

    private static BackupSourceSnapshot Snapshot(string install, bool running = false) => new()
    {
        InstallRoots = [Path.GetFullPath(install)],
        GameLoopRunning = running,
        Settings = new GameLoopSettings()
    };

    private sealed record LayoutPaths(
        string Root,
        string CacheDirectory,
        string CacheFile,
        string MetadataFile,
        string PackageFile,
        string ApkFile,
        string UnknownFile,
        ScriptedMarket Market);

    private sealed record Harness(AppMarketRepairService Service, RecordingLog Log);

    private sealed class ScriptedMarket : IAppMarketDiagnostics
    {
        public AppMarketReport Report { get; set; } = new();

        public Task<OperationResult<AppMarketReport>> ScanAsync(bool checkOfficialVersion, CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult<AppMarketReport>.Success(Report));
    }

    private sealed class WindowList : IWindowTitleSource
    {
        private readonly string? _title;

        public WindowList(string? title = null) => _title = title;

        public OperationResult<IReadOnlyList<WindowTitle>> List() =>
            _title is null
                ? OperationResult<IReadOnlyList<WindowTitle>>.Success([])
                : OperationResult<IReadOnlyList<WindowTitle>>.Success([new WindowTitle { ProcessId = 1, Title = _title }]);
    }

    private sealed class FakeProcesses : IProcessControl
    {
        public List<ControlledProcess> Items { get; } = [];

        public List<int> Closed { get; } = [];

        public List<int> Terminated { get; } = [];

        public int ListCalls { get; private set; }

        public int LeaveAfterLists { get; set; } = int.MaxValue;

        public string? OutsidePath { get; set; }

        public void ResetCounting() => ListCalls = 0;

        public IReadOnlyList<ControlledProcess> List()
        {
            ListCalls++;
            if (ListCalls >= LeaveAfterLists && OutsidePath is not null)
            {
                return Items.Select(item => new ControlledProcess
                {
                    ProcessId = item.ProcessId,
                    ProcessName = item.ProcessName,
                    ExecutablePath = item.ProcessName.Equals("GameLoop", StringComparison.OrdinalIgnoreCase) ? OutsidePath : item.ExecutablePath
                }).ToArray();
            }

            return Items.ToArray();
        }

        public bool TryCloseMainWindow(int processId)
        {
            Closed.Add(processId);
            return true;
        }

        public bool TryTerminate(int processId)
        {
            Terminated.Add(processId);
            Items.RemoveAll(item => item.ProcessId == processId);
            return true;
        }
    }

    private sealed class CorruptCopy : IFileTransfer
    {
        public void Copy(string source, string destination) => File.WriteAllBytes(destination, [1, 2, 3]);

        public void Move(string source, string destination) => File.Move(source, destination);
    }

    private sealed class NoDelay : ISampleDelay
    {
        public Task WaitAsync(TimeSpan interval, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new DateTimeOffset(2026, 9, 26, 18, 0, 0, TimeSpan.Zero);
    }

    private sealed class RecordingLog : ILogStore
    {
        public List<string> Lines { get; } = [];

        public string LogDirectory => string.Empty;

        public string ActiveLogFilePath => string.Empty;

        public LogSeverity MinimumLevel => LogSeverity.Information;

        public string? LastError => null;

        public void ApplyPolicy(AppSettings settings)
        {
        }

        public void Write(LogSeverity severity, string category, string message, Exception? exception = null) =>
            Lines.Add(category + " " + message);

        public IReadOnlyList<LogEntry> GetRecent(int count = 200) => [];

        public IReadOnlyList<LogEntry> ReadActiveLog(int maxLines = 500) => [];

        public void Flush()
        {
        }
    }

    private sealed class FixedSource : IBackupSource
    {
        public FixedSource(BackupSourceSnapshot snapshot) => Snapshot = snapshot;

        public BackupSourceSnapshot Snapshot { get; set; }

        public Task<BackupSourceSnapshot> CaptureAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Snapshot);
    }

    private sealed class TempTree : IDisposable
    {
        public TempTree()
        {
            Root = Path.Combine(Path.GetTempPath(), "glopt-repair-" + Guid.NewGuid().ToString("N"));
            AppData = Path.Combine(Root, "appdata");
            Directory.CreateDirectory(AppData);
            Backups = Path.Combine(AppData, "GLOptimizer", "Backups");
        }

        public string Root { get; }

        public string AppData { get; }

        public string Backups { get; }

        public string Dir(string name)
        {
            var path = Path.Combine(Root, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, true);
            }
            catch (Exception)
            {
                // The temp tree is best-effort.
            }
        }
    }
}

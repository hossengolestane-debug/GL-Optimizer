# Phase 11 — Stabilization

## Shutdown and handles

`FileLogStore`, `WindowsSystemMonitor`, and `WindowsProcessProbe` are registered as concrete singletons so the container disposes the log writer, performance counters, and process handles. `PageViewModelFactory` disposes cached page view models, which unsubscribe from monitoring. `MainViewModel` unsubscribes from toast and navigation. The sampler catches exceptions from `Sampled` so a UI handler cannot fault the sample task. Settings load and save take a lock. `LaunchSessionWatcher` cancels and disposes its wait, and a second dispose is ignored.

## Recovery

Startup offers one action, in this order:

1. Interrupted App Market repair (`repair-checkpoint.json` with `InProgress`). Rollback restores only quarantine files whose relative path lands under a saved install root. `..`, rooted paths, and reparse points are refused. Dismiss hides the banner and leaves the checkpoint.
2. Interrupted optimization (`last-optimization.json` with `InProgress`). The record is written before any config edit. A successful automatic restore clears it. The banner calls the existing confirmed undo. Dismiss hides the banner and leaves the backup.
3. Launch Optimized journal. Restore puts saved priorities back. Dismiss clears the journal without changing priorities.

A completed repair checkpoint (`AwaitingRefresh`, `InProgress` false) is not treated as interrupted.

## Corrupt files

Unreadable `settings.json`, `repair-checkpoint.json`, `launch-optimized.json`, `last-optimization.json`, and backup manifests fall back with a warning and do not throw. The damaged file is left in place except when the user clears a checkpoint or a successful restore deletes the optimization record.

## Missing GameLoop and unknown versions

Dashboard status is "GameLoop was not found." or a count of installs. Version and hardware fields stay Unknown when they were not read. Pages do not invent an official version. Multiple installs stay listed. This was not exercised against a real GameLoop install.

## Idle CPU

Verified by tests and by reading the startup path, not by a profiler on hardware:

- `MonitoringCoordinator` does not start the sampler when `MonitoringEnabled` is false. That case is covered by `Coordinator_does_not_sample_when_monitoring_is_disabled`.
- `LaunchSessionWatcher.Start` returns without scheduling a delay when no journal exists. `Launch_watcher_does_not_poll_when_no_journal_exists` asserts `IsWatching` is false, then true only after a journal is saved.
- Minimizing the window hides it. It does not start a timer. Sampling still follows the page and the monitoring switch.
- `--smoke-test` sets `SmokeTest.Active`, which blocks the sampler and the watcher. Page refreshes still run, including the read-only scan used when GameLoop is not installed.

## Startup crash

`Button.Chrome` and the text styles set `Foreground` with `{StaticResource Brush.Text.Primary}`. Those setters live in `Controls.xaml` and `Typography.xaml`. A `StaticResource` inside a style setter resolves only in that dictionary and the dictionaries it merges, not in a sibling merged by `Theme.xaml`. The deferred reference became `DependencyProperty.UnsetValue`, and the first `TextBlock` measure during `Window.Show` threw. `Colors.xaml` is now merged into both dictionaries. `--smoke-test` shows the window off-screen, measures and arranges it, opens every page, and fails on a binding trace, a resource exception, or a missing "GameLoop was not found." result. A page that throws during measure is logged and replaced in that frame; the shell stays up, and the smoke test still fails.

## Layout

Button rows use `WrapPanel`. The optimize table scrolls horizontally. Settings descriptions wrap, and the path box uses a max width. The window minimum stays 1100×700 device-independent pixels. Per-monitor DPI v2 is set on the WPF project. 125%, 150%, and 200% scaling were not launched in this environment.

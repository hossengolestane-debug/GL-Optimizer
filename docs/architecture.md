# Architecture

The Windows UI sits on class libraries that build on Windows, Linux, and macOS.

## Projects

- **GLOptimizer.Core** has no NuGet dependencies. It owns `OperationResult`, path and version rules, the hardware report builder, diagnostic state, settings rules, log line formatting, the page catalog, and the service interfaces.
- **GLOptimizer.Infrastructure** is the composition helper for JSON settings, the rolling file log, navigation state, file backups, and the optimization record.
- **GLOptimizer.Monitoring** queries hardware through `WindowsHardwareProbe` and maps that snapshot in `HardwareDetector`. `PerformanceSampler` reads CPU, RAM, disk, and GPU on a timer while the dashboard or Monitoring page is active. Frame metrics stay unimplemented.
- **GLOptimizer.GameLoop** searches for installs in `GameLoopDetector` and reads configuration in `GameLoopConfigDiscovery`. `GameLoopBackupSource` turns that report into the file list a backup may copy. `OptimizationEngine` previews and applies changes to keys that report already parsed. `GameLoopLauncher` starts a verified executable. Close and restart stop only processes inside that install, and force stop requires a second confirmation. `AppMarketDiagnostics` and `CodMobileDiagnostics` read a verified install only. `AppMarketRepairService` moves re-validated cache into a backup quarantine. `IOfficialVersionSource` does not contact a host.
- **GLOptimizer.App** is the WPF composition root. `AppHost` builds the `ServiceProvider`, loads settings, then shows `MainWindow`. Page view models are created by `PageViewModelFactory`. Scans are async and cancellable. Code-behind is limited to window chrome, the work-area maximize hook, and the error dialog.

Windows-only APIs live behind `OperatingSystem.IsWindows()` and `[SupportedOSPlatform("windows")]`. Tests use fixture environments and never attach to a real GameLoop install.

## Results, not fake numbers

Services return `OperationResult` or `OperationResult<T>`.

- `Success` carries a value the service actually produced. Individual fields inside that value may still be null.
- `NotImplemented` carries no value.
- `Failed` carries a user-facing error. A cancelled scan is a failure with no invented install list.

`HardwareText` prints **Unknown** when a field was not reported. `DiagnosticAssessment` returns GOOD only when a verified GameLoop install exists and the hardware report includes a CPU name or a total memory size. GameLoop not found is WARNING. A broken uninstall entry with no verified install, or both scans failing, is ACTION REQUIRED.

## Detection

`IHardwareProbe.Capture` is synchronous and runs on a background thread. WMI calls cannot be aborted mid-query; cancellation is checked between queries.

`PerformanceSampler` publishes `MetricSample` values from a background timer. View models copy them onto the UI thread. The sampler stops when monitoring is turned off, when the user leaves the dashboard and Monitoring pages, and when the process exits.

`IGameLoopEnvironment` supplies uninstall hints, candidate directories, Start Menu targets, process paths, and bounded directory walks. `GameLoopDetector` accepts a directory only when a known launcher file exists under it. Process rows are included only when the executable path is inside that install.

`GameLoopConfigDiscovery` checks a fixed list of config, key-map, and registry locations for those verified roots. It parses only small text files and only published keys. Null settings stay Unknown. It does not write.

`FileBackupService` copies only files that source marked present and that sit under a verified install or a known user config directory. Restore writes those recorded paths after a dry run, a hash check, a pre-restore backup, and confirmation. It refuses traversal, reparse points, and a running GameLoop. Registry values in the manifest stay text.

`OptimizationRules` classifies hardware and recommends values without writing. `ConfigTextEditor` replaces one value in ini, json, or xml and leaves the rest of the file in place. `OptimizationEngine` backs up with `FileBackupService`, writes through a temp file, re-reads the keys, and restores the backup when validation fails.

`AppMarketDetector` walks a verified install with a depth limit, an entry limit, and no reparse following. `PackageVersion` accepts only a 2–4 part numeric version. `CatalogComparisonLogic` can return MATCH, VERSION MISMATCH, LOCAL MARKET OUTDATED, or UNKNOWN. It does not return REMOTE CATALOG ISSUE. `GrayScreenAnalyzer` ranks findings from evidence that was already collected. `UnavailableOfficialVersionSource` returns no version and does not open a connection. Check Version is the only caller, and it logs the host when a future source names one.

`AppMarketRepairRules` accepts only cache that is under the verified root, is not a reparse point, and is not a package, key map, APK, OBB, or XAPK. Metadata files are copied, not moved. `QuarantineStore` moves those cache files into the backup quarantine, or copies and hash-checks them when the backup is on another volume. A failed move restores files already placed. `RepairVerdictLogic` compares the market version saved before the repair with the version read after the user reopens GameLoop. REMOTE CATALOG ISSUE is produced only by that second read. `GameLoopSessionStopper` selects GameLoop-family processes whose executable path is inside the verified install. `FileBackupService` restores the quarantine with the same path checks and refuses the restore while GameLoop is running.

`PubgMobileDiagnostics` uses the same version reader and `CatalogComparisonLogic`, passing `officialRaw: null`. `LaunchOptimizedService` plans `AboveNormal` through `ProcessSelection` and stores the previous priority in `JsonLaunchJournalStore`. `LaunchSessionWatcher` clears that journal after the recorded processes exit. `WindowsStartupRegistration` touches only `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `GL Optimizer`. `NotImplementedUpdateService` validates nothing on the network. `NetworkDiagnosticsService` probes the allowlist only when the Diagnostics page runs it. `ReportRedaction` replaces the user-profile path. `DeveloperSimulation` returns values only in Debug builds.

## Logging

`FileLogStore` writes one tab-separated record per line, keeps a short in-memory buffer for the session, and rotates `gloptimizer.log` by size. Archive names embed a UTC timestamp so retention does not depend on filesystem creation time. `FileLoggerProvider` forwards `ILogger` calls into the same store.

## Startup

1. Create `%LocalAppData%\GLOptimizer` and `Logs`.
2. Load or create `settings.json`. A corrupt file falls back to defaults and is reported in the log.
3. Apply the log level, size, and retention.
4. Resolve `MainWindow`. The dashboard starts a read-only hardware and GameLoop scan. If `FirstRunCompleted` is false, the welcome scan is shown once. If `launch-optimized.json` is still present, recovery is offered. `LaunchSessionWatcher` starts with the process.

Unhandled dispatcher exceptions are logged and shown in `ErrorWindow`. The dialog uses `UserFacingError`, not the stack trace.

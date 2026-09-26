# Architecture

Phase 0 splits the product so the Windows UI can sit on code that builds anywhere.

## Projects

- **GLOptimizer.Core** has no NuGet dependencies. It owns `OperationResult`, path rules, settings rules, log line formatting, page catalog, and the service interfaces.
- **GLOptimizer.Infrastructure** is the composition helper for JSON settings, the rolling file log, navigation state, and the backup/optimization stubs.
- **GLOptimizer.GameLoop** and **GLOptimizer.Monitoring** register Phase 0 stubs. Replacing a stub later is a registration change, not a UI rewrite.
- **GLOptimizer.App** is the WPF composition root. `AppHost` builds the `ServiceProvider`, loads settings, then shows `MainWindow`. Page view models are created by `PageViewModelFactory`. Code-behind is limited to window chrome, the work-area maximize hook, and the error dialog.

## Results, not fake numbers

Services return `OperationResult` or `OperationResult<T>`.

- `Success` carries a value the service actually produced.
- `NotImplemented` carries no value.
- `Failed` carries a user-facing error.

`ReportedValue` prints a CPU name, FPS, GameLoop version, or backup count only when that field is present on a successful result. Otherwise the UI shows "—".

## Logging

`FileLogStore` writes one tab-separated record per line, keeps a short in-memory buffer for the session, and rotates `gloptimizer.log` by size. Archive names embed a UTC timestamp so retention does not depend on filesystem creation time. `FileLoggerProvider` forwards `ILogger` calls into the same store.

## Startup

1. Create `%LocalAppData%\GLOptimizer` and `Logs`.
2. Load or create `settings.json`. A corrupt file falls back to defaults and is reported in the log.
3. Apply the log level, size, and retention.
4. Resolve `MainWindow`. The dashboard reads the stubs and the real log.

Unhandled dispatcher exceptions are logged and shown in `ErrorWindow`. The dialog uses `UserFacingError`, not the stack trace.

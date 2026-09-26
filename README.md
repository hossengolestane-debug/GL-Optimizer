# GL Optimizer

GameLoop Performance & App Market Utility.

Phase 0 is the foundation: a Windows desktop shell, local settings, logging, and navigation. It is a diagnostics and configuration utility. It does not tune a game, and it does not pretend to.

## What this build does

- Opens a custom-chrome window (default 1280×800, minimum 1100×700) with a collapsible sidebar.
- Navigates to Dashboard, Optimize, Monitoring, GameLoop, App Market, COD Mobile, PUBG Mobile, Diagnostics, Backups, Logs, and Settings.
- Shows status from real service results. Phase 0 services return **not implemented**, so cards stay at "—" instead of invented hardware, FPS, or health scores.
- Writes JSON settings to `%LocalAppData%\GLOptimizer\settings.json`.
- Writes rolling logs to `%LocalAppData%\GLOptimizer\Logs\`.
- Catches unhandled UI exceptions and shows a short message. The stack trace goes to the log.

**OPTIMIZE NOW** stays disabled. Backup, GameLoop detection, App Market checks, hardware inventory, and frame metrics are stubs. They do not read or write GameLoop files, App Market files, or game clients.

## Safety

GL Optimizer must not:

- inject into a game process
- read or write game memory
- bypass anti-cheat
- spoof hardware IDs
- patch game binaries
- imitate GameLoop server responses

Phase 0 also must not touch GameLoop configuration or App Market files. The stubs return `OperationStatus.NotImplemented` and do not query devices or guess install paths. See [docs/safety.md](docs/safety.md).

Developer tools (a debug log button and local path display) are compiled only into Debug builds (`#if DEBUG`).

## Windows target

- Windows 10 or Windows 11, 64-bit
- .NET 8 SDK to build
- .NET 8 Desktop Runtime to run the framework-dependent publish
- Per-monitor DPI v2 is declared in the application manifest

The UI project is WPF (`net8.0-windows`). Class libraries and tests target `net8.0` and build on Windows, Linux, and macOS.

## Build and publish

From the repository root, on Windows:

```powershell
dotnet test .\GLOptimizer.sln -c Release
dotnet publish .\src\GLOptimizer.App\GLOptimizer.App.csproj -c Release -r win-x64 --self-contained false
```

`scripts\build-windows.ps1` and `scripts\publish-windows.ps1` wrap those commands. The publish profile `win-x64.pubxml` is framework-dependent. Output:

`src\GLOptimizer.App\bin\Release\net8.0-windows\win-x64\publish\GLOptimizer.exe`

The WPF project sets `EnableWindowsTargeting`, so the full solution also compiles on Linux and macOS. That produces a Windows binary. Launching the window still requires Windows 10 or Windows 11.

```bash
dotnet test GLOptimizer.sln -c Release
```

`scripts/build-libs.sh` runs that command. `GLOptimizer.NonWindows.slnf` builds only the class libraries and tests when the Windows targeting pack is not available. There is no installer in this phase. See [installer/README.md](installer/README.md).

## Solution layout

```
GLOptimizer.sln
src/
  GLOptimizer.App/             WPF shell, theme, pages, composition root
  GLOptimizer.Core/            models, results, paths, interfaces
  GLOptimizer.Infrastructure/  DI, JSON settings, rolling file log
  GLOptimizer.GameLoop/        Phase 0 stubs only
  GLOptimizer.Monitoring/      Phase 0 stubs only
  GLOptimizer.Tests/
installer/                     not shipped yet
assets/                        monochrome mark
docs/
scripts/
```

Settings and logs:

| Item | Path |
| --- | --- |
| Root | `%LocalAppData%\GLOptimizer\` |
| Settings | `%LocalAppData%\GLOptimizer\settings.json` |
| Active log | `%LocalAppData%\GLOptimizer\Logs\gloptimizer.log` |
| Archives | `%LocalAppData%\GLOptimizer\Logs\gloptimizer-yyyyMMdd-HHmmssfff.log` |

Default retention is 14 days. The active log rotates after 2 MB. Both can be changed on the Settings page (1–90 days, 1–50 MB).

## Phase 1

Phase 1 should stay inside the safety boundary:

1. Read-only GameLoop install detection, with no config writes.
2. Read-only hardware inventory through documented Windows APIs, shown only when a query actually returns a value.
3. An explicit, user-confirmed backup of files the user selects. No silent restore.
4. A monitoring design that does not inject into a process. If a metric cannot be collected that way, the UI keeps showing "not implemented".
5. A Windows UI pass for snap layout, high contrast, and the installed package.
6. An MSIX or WiX installer.

Do not enable **OPTIMIZE NOW** until each action is a real, reviewed change that does not touch game memory, anti-cheat, or GameLoop servers.

More detail is in [docs/architecture.md](docs/architecture.md) and [docs/phase-1.md](docs/phase-1.md).

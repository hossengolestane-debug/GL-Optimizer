# GL Optimizer

GameLoop Performance & App Market Utility.

Phase 3 is a diagnostics utility: a Windows desktop shell, read-only hardware and GameLoop detection, live samples, and a read-only GameLoop configuration report. It does not tune a game, and it does not pretend to.

## What this build does

- Opens a custom-chrome window (default 1280×800, minimum 1100×700) with a collapsible sidebar.
- Navigates to Dashboard, Optimize, Monitoring, GameLoop, App Market, COD Mobile, PUBG Mobile, Diagnostics, Backups, Logs, and Settings.
- Shows hardware that Windows actually returned. Missing CPU, GPU, memory, storage, or refresh fields stay **Unknown**.
- Searches for GameLoop without assuming one install path. A directory counts only when a known launcher file is present. Version and engine stay empty unless they were read.
- Shows PUBG Mobile and COD Mobile as Installed, Not found, or Unknown. Versions come only from unambiguous local version files.
- Samples CPU, GPU (when the Windows counter exists), RAM, disk activity, and GameLoop process CPU, RAM, and state while the dashboard or Monitoring page is open. The interval is 500, 1000, or 2000 ms. Other pages do not keep the timer running.
- Shows a 5-minute monochrome history and simple 90% spikes. FPS stays unavailable: there is no safe frame provider, and no number is invented.
- Reports GameLoop configuration from verified installs only: known ini, json, and xml files, key maps, and the MobileGamePC registry key. Unrecognized settings stay **Unknown**. Nothing is written.
- Sets the dashboard to GOOD, WARNING, or ACTION REQUIRED from those findings. GameLoop not found is WARNING. There is no version-mismatch badge in this phase.
- Writes JSON settings to `%LocalAppData%\GLOptimizer\settings.json`.
- Writes rolling logs to `%LocalAppData%\GLOptimizer\Logs\`.
- Catches unhandled UI exceptions and shows a short message. The stack trace goes to the log.

**OPTIMIZE NOW** stays disabled. **Close** and **Restart** are not implemented and do not kill a process. **Start** launches only a launcher file inside a verified install. Backup, App Market checks, and frame metrics are still not implemented. This build does not write GameLoop files, registry values, or App Market files.

## Safety

GL Optimizer must not:

- inject into a game process
- read or write game memory
- bypass anti-cheat
- spoof hardware IDs
- patch game binaries
- imitate GameLoop server responses

Phase 3 reads install metadata, process paths, and configuration. It does not write GameLoop configuration or App Market files. See [docs/safety.md](docs/safety.md).

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
  GLOptimizer.GameLoop/        read-only GameLoop detection and configuration; App Market still unimplemented
  GLOptimizer.Monitoring/      hardware detection and live sampling; FPS still unavailable
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

## Phases

Phase 1 detection is described in [docs/phase-1.md](docs/phase-1.md). Phase 2 live sampling is described in [docs/phase-2.md](docs/phase-2.md). Phase 3 configuration discovery is described in [docs/phase-3.md](docs/phase-3.md). On Linux and macOS the solution compiles and the tests run, but WMI, performance counters, the registry, and the WPF window do not. Those hosts report architecture only and do not invent CPU, GPU, memory, FPS, or GameLoop settings.

Phase 4 is backup and restore of files the configuration report already found. **OPTIMIZE NOW** stays disabled. See [docs/phase-4.md](docs/phase-4.md).

More detail is in [docs/architecture.md](docs/architecture.md).

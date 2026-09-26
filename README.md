# GL Optimizer

GameLoop Performance & App Market Utility.

Phase 8 adds a confirmed App Market repair that moves only re-validated cache into a backup quarantine. The earlier pipeline remains: hardware and GameLoop detection, live samples, a configuration report, backups, writes that change only keys already found in a parsed config file, and read-only COD Mobile diagnostics. It does not tune a running game, invent a frame rate, or change GameLoop's remote catalog.

## What this build does

- Opens a custom-chrome window (default 1280×800, minimum 1100×700) with a collapsible sidebar.
- Navigates to Dashboard, Optimize, Monitoring, GameLoop, App Market, COD Mobile, PUBG Mobile, Diagnostics, Backups, Logs, and Settings.
- Shows hardware that Windows actually returned. Missing CPU, GPU, memory, storage, or refresh fields stay **Unknown**.
- Searches for GameLoop without assuming one install path. A directory counts only when a known launcher file is present. Version and engine stay empty unless they were read.
- Shows PUBG Mobile and COD Mobile as Installed, Not found, or Unknown. Versions come only from unambiguous local version files.
- Samples CPU, GPU (when the Windows counter exists), RAM, disk activity, and GameLoop process CPU, RAM, and state while the dashboard or Monitoring page is open. The interval is 500, 1000, or 2000 ms. Other pages do not keep the timer running.
- Shows a 5-minute monochrome history and simple 90% spikes. FPS stays unavailable: there is no safe frame provider, and no number is invented.
- Reports GameLoop configuration from verified installs only: known ini, json, and xml files, key maps, and the MobileGamePC registry key. Unrecognized settings stay **Unknown**. Discovery does not write.
- Creates a backup of those found files under `%LocalAppData%\GLOptimizer\Backups`, with a manifest, hashes, a settings snapshot, and registry values as text. Restore shows a dry run, asks for confirmation, writes a pre-restore backup, and refuses unsafe paths and a running GameLoop.
- Recommends Performance, Balanced, or Quality values from the hardware tier. **OPTIMIZE NOW** previews the diff, creates a Phase 4 backup, writes only existing keys, validates the read-back, and restores the backup if validation fails. Registry settings, new keys, and new files are not written. Graphics preference, process priority, power mode, background apps, and benchmark mode are Not implemented.
- Sets the dashboard to GOOD, WARNING, or ACTION REQUIRED from those findings. GameLoop not found is WARNING. The COD Mobile card shows MATCH, VERSION MISMATCH, LOCAL MARKET OUTDATED, or the install presence when the comparison is Unknown. It does not invent a mismatch.
- Reads App Market files under a verified install: executable, cache directories, metadata, and COD Mobile package metadata, with a depth and file-count limit. SQLite metadata is opened read-only. **CHECK AGAIN** does not use the network. **Check Version** is the only control that asks for an official version, and that source is not implemented, so the official version stays Unknown.
- **DRY RUN REPAIR** lists what would stop, what metadata would be copied, and which cache files would move. **REPAIR APP MARKET** asks for confirmation, stops only GameLoop processes inside the verified install, copies metadata, and moves cache into `%LocalAppData%\GLOptimizer\Backups\<id>\quarantine`. Metadata, packages, game data, APKs, OBBs, key maps, and unknown files are not removed. A set larger than 5 GB or 20,000 files, or any unexpected cache path, stops in the dry run. Cancel is available until the first move; after that the repair finishes or rolls the moved files back. **Start GameLoop** is offered afterward and is not launched automatically. **RE-CHECK AFTER GAMELOOP REFRESH** compares the saved pre-repair market version with the version read after GameLoop is opened again. A matching or newer market version is a local refresh. The same outdated version is reported as a remote catalog issue and is not modified. The success toast is "GameLoop App Market repair completed." and is written only when the move finishes. Restoring that backup from the Backups page puts the quarantined cache back and refuses to run while GameLoop is running.
- Collects gray-screen evidence for COD Mobile: GameLoop and engine processes, a COD process or window under the verified install, a short CPU sample, the Phase 3 renderer, cache timestamps, and local engine log lines. Findings cite that evidence. Open in GameLoop, Restart Engine, and repair stay disabled.
- Writes JSON settings to `%LocalAppData%\GLOptimizer\settings.json`.
- Writes rolling logs to `%LocalAppData%\GLOptimizer\Logs\`.
- Catches unhandled UI exceptions and shows a short message. The stack trace goes to the log.

**OPTIMIZE NOW** asks for confirmation, then runs preview, backup, apply, and validate. It is disabled when that profile has no applicable recommendation. **Close** and **Restart** ask for confirmation, then close only processes whose executable path is inside the verified install. Force stop asks a second time. Unrelated processes are not touched. **Start** launches only a launcher file inside a verified install. Benchmark mode and frame metrics are still not implemented. This build does not write registry values. It writes a GameLoop config file only for a confirmed restore or a confirmed optimization of a key that was already in that file. App Market cache is moved into a backup quarantine, not deleted, and only after the checks above.

## Safety

GL Optimizer must not:

- inject into a game process
- read or write game memory
- bypass anti-cheat
- spoof hardware IDs
- patch game binaries
- imitate GameLoop server responses

The build reads install metadata, process paths, configuration, and App Market files under a verified install. It can restore a confirmed backup, replace values that were already present after a backup and a validation check, and move re-validated App Market cache into a restorable quarantine. It does not write registry values or change GameLoop's remote catalog. See [docs/safety.md](docs/safety.md).

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
  GLOptimizer.Infrastructure/  DI, JSON settings, rolling file log, file backup and restore
  GLOptimizer.GameLoop/        GameLoop detection, configuration, backup source, optimization, App Market inventory, and quarantined cache repair
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
| Backups | `%LocalAppData%\GLOptimizer\Backups\<BackupId>\manifest.json` |
| Repair checkpoint | `%LocalAppData%\GLOptimizer\repair-checkpoint.json` |
| Repair quarantine | `%LocalAppData%\GLOptimizer\Backups\<BackupId>\quarantine\` |

Default retention is 14 days. The active log rotates after 2 MB. Both can be changed on the Settings page (1–90 days, 1–50 MB).

## Phases

Phase 1 detection is described in [docs/phase-1.md](docs/phase-1.md). Phase 2 live sampling is described in [docs/phase-2.md](docs/phase-2.md). Phase 3 configuration discovery is described in [docs/phase-3.md](docs/phase-3.md). Phase 4 backup and restore is described in [docs/phase-4.md](docs/phase-4.md). Phase 5 optimization is described in [docs/phase-5.md](docs/phase-5.md). Phase 6 COD Mobile diagnostics is described in [docs/phase-6.md](docs/phase-6.md). Phase 7 App Market diagnostics is described in [docs/phase-7.md](docs/phase-7.md). Phase 8 App Market repair is described in [docs/phase-8.md](docs/phase-8.md). Phase 9 PUBG Mobile diagnostics is planned in [docs/phase-9.md](docs/phase-9.md) and is not implemented. On Linux and macOS the solution compiles and the tests run, but WMI, performance counters, the registry, and the WPF window do not. Those hosts report architecture only and do not invent CPU, GPU, memory, FPS, or GameLoop settings.

More detail is in [docs/architecture.md](docs/architecture.md).

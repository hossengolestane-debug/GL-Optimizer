# GL Optimizer

GameLoop Performance & App Market Utility.

Phase 11 stabilizes recovery, corrupt local files, and shutdown. Phase 10 adds a Program Files installer. The app still does not tune a running game, invent a frame rate, or change GameLoop's remote catalog.

## What this build does

- Opens a custom-chrome window (default 1280×800, minimum 1100×700) with a collapsible sidebar.
- Navigates to Dashboard, Optimize, Monitoring, GameLoop, App Market, COD Mobile, PUBG Mobile, Diagnostics, Backups, Logs, Activity, and Settings.
- Shows hardware that Windows actually returned. Missing CPU, GPU, memory, storage, or refresh fields stay **Unknown**.
- Searches for GameLoop without assuming one install path. A directory counts only when a known launcher file is present. Version and engine stay empty unless they were read.
- Shows PUBG Mobile and COD Mobile as Installed, Not found, or Unknown. Versions come only from unambiguous local version files. PUBG also compares that version with App Market metadata already found, plus process, window, CPU, and local log evidence. The official PUBG version stays Unknown.
- Samples CPU, GPU (when the Windows counter exists), RAM, disk activity, and GameLoop process CPU, RAM, and state while monitoring is enabled and the dashboard or Monitoring page is open. The interval is 500, 1000, or 2000 ms. Other pages do not keep the timer running. Turning monitoring off stops it.
- Shows a 5-minute monochrome history and simple 90% spikes. FPS stays unavailable: there is no safe frame provider, and no number is invented.
- Reports GameLoop configuration from verified installs only: known ini, json, and xml files, key maps, and the MobileGamePC registry key. Unrecognized settings stay **Unknown**. Discovery does not write.
- Creates a backup of those found files under `%LocalAppData%\GLOptimizer\Backups`, with a manifest, hashes, a settings snapshot, and registry values as text. Restore shows a dry run, asks for confirmation, writes a pre-restore backup, and refuses unsafe paths and a running GameLoop.
- Recommends Performance, Balanced, or Quality values from the hardware tier. **OPTIMIZE NOW** previews the diff, creates a Phase 4 backup, writes only existing keys, validates the read-back, and restores the backup if validation fails. Registry settings, new keys, and new files are not written. Graphics preference, process priority, power mode, background apps, and benchmark mode are Not implemented.
- Sets the dashboard to GOOD, WARNING, or ACTION REQUIRED from those findings. GameLoop not found is WARNING. The COD Mobile card shows MATCH, VERSION MISMATCH, LOCAL MARKET OUTDATED, or the install presence when the comparison is Unknown. It does not invent a mismatch.
- Reads App Market files under a verified install: executable, cache directories, metadata, and COD Mobile package metadata, with a depth and file-count limit. SQLite metadata is opened read-only. **CHECK AGAIN** does not use the network. **Check Version** is the only control that asks for an official version, and that source is not implemented, so the official version stays Unknown.
- **DRY RUN REPAIR** lists what would stop, what metadata would be copied, and which cache files would move. **REPAIR APP MARKET** asks for confirmation, stops only GameLoop processes inside the verified install, copies metadata, and moves cache into `%LocalAppData%\GLOptimizer\Backups\<id>\quarantine`. Metadata, packages, game data, APKs, OBBs, key maps, and unknown files are not removed. A set larger than 5 GB or 20,000 files, or any unexpected cache path, stops in the dry run. Cancel is available until the first move; after that the repair finishes or rolls the moved files back. **Start GameLoop** is offered afterward and is not launched automatically. **RE-CHECK AFTER GAMELOOP REFRESH** compares the saved pre-repair market version with the version read after GameLoop is opened again. A matching or newer market version is a local refresh. The same outdated version is reported as a remote catalog issue and is not modified. The success toast is "GameLoop App Market repair completed." and is written only when the move finishes. Restoring that backup from the Backups page puts the quarantined cache back and refuses to run while GameLoop is running.
- Collects gray-screen evidence for COD Mobile: GameLoop and engine processes, a COD process or window under the verified install, a short CPU sample, the Phase 3 renderer, cache timestamps, and local engine log lines. Findings cite that evidence. Open in GameLoop, Restart Engine, and repair stay disabled.
- Writes JSON settings to `%LocalAppData%\GLOptimizer\settings.json`. Start with Windows is the only registry write, and it is the HKCU Run value named `GL Optimizer`, saved only when the user turns it on. Launch Optimized can set AboveNormal on verified GameLoop processes and records that in `launch-optimized.json` until those processes exit or the next startup restores it. Realtime is never set.
- Writes rolling logs to `%LocalAppData%\GLOptimizer\Logs\`.
- Catches unhandled UI exceptions and shows a short message. The stack trace goes to the log.

**OPTIMIZE NOW** asks for confirmation, then runs preview, backup, apply, and validate. It is disabled when that profile has no applicable recommendation. **Close** and **Restart** ask for confirmation, then close only processes whose executable path is inside the verified install. Force stop asks a second time. Unrelated processes are not touched. **Start** launches only a launcher file inside a verified install. **Launch Optimized** changes only the priority of those verified processes to AboveNormal. Benchmark mode, frame metrics, power plan changes, graphics preference changes, Light theme, and the self-update download are still not implemented. GameLoop config is written only for a confirmed restore or a confirmed optimization of a key that was already in that file. App Market cache is moved into a backup quarantine, not deleted, and only after the checks above.

## Safety

GL Optimizer must not:

- inject into a game process
- read or write game memory
- bypass anti-cheat
- spoof hardware IDs
- patch game binaries
- imitate GameLoop server responses

The build reads install metadata, process paths, configuration, and App Market files under a verified install. It can restore a confirmed backup, replace values that were already present after a backup and a validation check, move re-validated App Market cache into a restorable quarantine, and set AboveNormal on a verified GameLoop process after confirmation. The only registry write is the user-initiated HKCU Run value for Start with Windows. It does not change GameLoop's remote catalog. See [docs/safety.md](docs/safety.md).

Developer tools (a debug log button and local path display) are compiled only into Debug builds (`#if DEBUG`).

## Windows target

- Windows 10 or Windows 11, 64-bit
- .NET 8 SDK to build
- .NET 8 is included in the self-contained publish used by the installer
- Per-monitor DPI v2 is set with `ApplicationHighDpiMode` on the WPF project. The executable manifest stays `asInvoker`

The UI project is WPF (`net8.0-windows`). Class libraries and tests target `net8.0` and build on Windows, Linux, and macOS.

## Build and publish

From the repository root, on Windows:

```powershell
dotnet test .\GLOptimizer.sln -c Release
.\scripts\publish-windows.ps1
```

`scripts\publish-windows.ps1` publishes a self-contained win-x64 folder (not a single file) to `publish\GLOptimizer.exe`. A framework-dependent publish, if you already have the .NET 8 Desktop Runtime, is:

```powershell
dotnet publish .\src\GLOptimizer.App\GLOptimizer.App.csproj -c Release -r win-x64 --self-contained false
```

Compile the installer after the folder publish. Inno Setup 6 must be installed:

```powershell
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" "/DMyAppVersion=0.1.0" installer\GLOptimizer.iss
```

Output is `installer\output\GL-Optimizer-Setup.exe`. Setup asks for administrator rights and installs under Program Files. The installed app still runs as the user. See [installer/README.md](installer/README.md) and [docs/phase-10.md](docs/phase-10.md).

The WPF project sets `EnableWindowsTargeting`, so the full solution also compiles on Linux and macOS. That produces a Windows binary. Launching the window still requires Windows 10 or Windows 11.

```bash
dotnet test GLOptimizer.sln -c Release
```

`scripts/build-libs.sh` runs that command. `GLOptimizer.NonWindows.slnf` builds only the class libraries and tests when the Windows targeting pack is not available.

## CI artifacts

`.github/workflows/build.yml` runs `dotnet test` on Ubuntu. `.github/workflows/windows-installer.yml` runs on `windows-latest` for push, pull request, and manual dispatch. It builds, tests, publishes, compiles the installer, and smoke-tests install, `--smoke-test`, and uninstall.

On a successful run, open the workflow run on GitHub and download:

| Artifact | Contents |
| --- | --- |
| `GL-Optimizer-Setup` | `GL-Optimizer-Setup.exe` |
| `publish` | self-contained `publish\GLOptimizer.exe` and its folder |

## Troubleshooting

- Setup says GL Optimizer is running. Close the window from the tray Exit command, or let setup close `GLOptimizer.exe`. The process holds a mutex named `GLOptimizer`.
- Silent uninstall kept `%LocalAppData%\GLOptimizer`. That is the default. Run the uninstaller from Settings and choose Yes to remove backups, logs, and settings.
- A corrupt `settings.json`, repair checkpoint, Launch Optimized journal, or optimization record is left on disk and the app continues with a warning. Rename the file if you want a clean default.
- GameLoop was not found, or more than one install is listed. Pick the install on the GameLoop page. An unknown version stays Unknown.
- Official COD and PUBG versions stay Unknown. There is no official version source in this build.

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
installer/                     Inno Setup script; compiled on Windows
publish/                       self-contained win-x64 output (not committed)
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
| Launch Optimized journal | `%LocalAppData%\GLOptimizer\launch-optimized.json` |

Default retention is 14 days. The active log rotates after 2 MB. Both can be changed on the Settings page (1–90 days, 1–50 MB).

## Phases

Phase 1 detection is described in [docs/phase-1.md](docs/phase-1.md). Phase 2 live sampling is described in [docs/phase-2.md](docs/phase-2.md). Phase 3 configuration discovery is described in [docs/phase-3.md](docs/phase-3.md). Phase 4 backup and restore is described in [docs/phase-4.md](docs/phase-4.md). Phase 5 optimization is described in [docs/phase-5.md](docs/phase-5.md). Phase 6 COD Mobile diagnostics is described in [docs/phase-6.md](docs/phase-6.md). Phase 7 App Market diagnostics is described in [docs/phase-7.md](docs/phase-7.md). Phase 8 App Market repair is described in [docs/phase-8.md](docs/phase-8.md). Phase 9 PUBG Mobile diagnostics and the spec-gap pass are described in [docs/phase-9.md](docs/phase-9.md). The installer is described in [docs/phase-10.md](docs/phase-10.md). Stabilization is described in [docs/phase-11.md](docs/phase-11.md). On Linux and macOS the solution compiles and the tests run, but WMI, performance counters, the registry, and the WPF window do not. Those hosts report architecture only and do not invent CPU, GPU, memory, FPS, or GameLoop settings.

## Known limitations

- Official GameLoop, COD Mobile, and PUBG Mobile versions are Unknown. No host is contacted for them.
- FPS is unavailable. No number is invented.
- Config key names were not checked against a real GameLoop install in this environment.
- Light theme, power plan changes, graphics preference changes, benchmark mode, and the self-update download are not implemented.
- Layout at 1100×700 and at 125/150/200% scaling was reviewed in XAML and was not launched on a Windows desktop here.

More detail is in [docs/architecture.md](docs/architecture.md).

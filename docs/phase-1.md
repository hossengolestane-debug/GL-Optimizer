# Phase 1 — Hardware and GameLoop detection

Phase 1 replaces the hardware and GameLoop stubs with read-only scans. It does not write GameLoop config, does not modify App Market files, does not kill processes, and does not fill missing values with guesses.

## Hardware

`WindowsHardwareProbe` queries, each in its own try/catch:

- `Win32_Processor` for the CPU name, core counts, and `VirtualizationFirmwareEnabled`
- `Win32_VideoController` for the GPU name, `AdapterRAM`, and `CurrentRefreshRate`
- `Win32_ComputerSystem` for `TotalPhysicalMemory` and `HypervisorPresent`
- `MSFT_PhysicalDisk` in `root\Microsoft\Windows\Storage` for `MediaType` (3 = HDD, 4 = SSD)
- `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion` for `ProductName`, `DisplayVersion`, `CurrentBuild`, and `UBR`
- `RuntimeInformation.OSArchitecture`

`HardwareReportBuilder` drops zeros and out-of-range core counts, refresh rates outside 20–1000 Hz, and VRAM that is zero or at least `0xFFFF0000` (the usual WMI garbage value for large adapters). `Microsoft Basic Display Adapter` is hidden only when another adapter name was returned. Storage text is only `HDD`, `SSD`, or `SSD + HDD`.

## GameLoop

The detector does not hardcode one directory. It collects candidates from:

- Uninstall keys under HKLM and HKCU, both 64-bit and 32-bit views, when `DisplayName` contains GameLoop or TxGameAssistant
- `Program Files`, `Program Files (x86)`, per-user, and ProgramData folders named `GameLoop` or `TxGameAssistant` on each fixed drive
- Running processes whose names match the known launcher or engine names, using only paths that could be read
- Start Menu `.lnk` files whose names match, resolved with `WScript.Shell` and ignored when resolution fails

A directory is an install only when one of these files exists under it: `GameLoop.exe`, `AppMarket.exe`, `TxGameAssistant.exe`, `AndroidEmulator.exe`, or `AndroidEmulatorEn.exe`. The preferred launcher is the first of those that exists. The engine is `AOW` only when `aow_exe.exe` is present at a known relative path. Several installs can be returned. Paths are full paths, and `..` cannot escape the install root.

Version comes from a numeric uninstall `DisplayVersion` (2–4 parts). If those conflict, the version is left empty. Otherwise the launcher file version is used when it matches the same pattern. Words such as `latest` are rejected.

Status is Running when a readable process path sits inside the install, Stopped when the process query succeeded and none did, and Unknown when the query failed or a matching process path could not be read.

## PUBG Mobile and COD Mobile

Package ids searched:

- PUBG: `com.tencent.ig`, `com.pubg.krmobile`, `com.rekoo.pubgm`, `com.vng.pubgmobile`, `com.tencent.tmgp.pubgmhd`
- COD: `com.activision.callofduty.shooter`, `com.garena.game.codm`, `com.tencent.tmgp.kr.codm`, `com.vng.codmvn`

The search looks at known relative data folders and then a bounded walk (depth 8, 2500 directories, reparse points skipped). If GameLoop itself was not found, both games stay Unknown. Not found is used only after the walk finishes with no match. Version files (`version.txt`, `version`, `info.json`, `config.ini`, `app.ini`, `manifest.json`) are read only in the package directory, up to 64 KB. Two different numeric versions become Unknown.

## UI

The dashboard, Monitoring, Diagnostics, GameLoop, PUBG, and COD pages start an async scan and can cancel it. The dashboard shows GOOD / WARNING / ACTION REQUIRED, hardware rows, install cards, and game cards (Ready, Installed, Not found, Unknown). **OPTIMIZE NOW** stays disabled. The GameLoop page lists processes for the selected install. Start is enabled only when that install has a launcher path. Close and Restart show the not-implemented message.

## Linux and other non-Windows hosts

The projects target `net8.0` so tests run in CI. `System.Management` and the registry are compiled in, and every call is behind a Windows platform check. On Linux the hardware report contains the process architecture and a warning. CPU, GPU, RAM, storage, refresh, and the Windows version stay empty. GameLoop uninstall keys and Start Menu shortcuts are skipped. Drive and process checks still run and usually find nothing, which the dashboard treats as WARNING. The WPF window is a Windows binary and is not launched here.

WMI on Windows can block a thread-pool thread until the call returns. Cancellation between queries does not abort a stuck WMI call. `AdapterRAM` is a 32-bit WMI property, so video memory above about 4 GB is often Unknown rather than a guessed size.

## Tests

Unit tests cover path normalization, version parsing, hardware sanity checks, diagnostic state, a fixture GameLoop tree, launcher refusal, and a non-destructive smoke scan of the current host. They do not write a GameLoop config and do not start a real emulator.

# Phase 6 — COD Mobile diagnostics

Phase 6 is read-only. It reports COD Mobile from a verified GameLoop install and collects gray-screen evidence. It does not delete files, write configuration, or stop a process.

## What it checks

Installed presence and the installed version come from the package folders and version files Phase 1 already searches. A version is shown only when `PackageVersion` accepts the whole string. Prerelease text and trailing words stay Unknown. `VersionText` is unchanged and is still the looser reader used by install detection.

The COD Mobile page shows Installed, GameLoop Market, Official detected, and Status. **Check Version** is the only control that asks `IOfficialVersionSource`. **Run Diagnostics** repeats the local checks and does not contact a host.

**Open in GameLoop**, **Restart Engine**, and **Repair Market** are visible and disabled. Restart would need a process kill. Repair would write. Neither is implemented.

## Gray-screen evidence

Each check is reported with the evidence behind it. A finding is not a probability and it is not a declared cause.

- GameLoop running, only when the process path is inside the verified install
- Android engine process (`aow_exe` or `AndroidEmulator`) under that install
- A COD Mobile process, or a window titled Call of Duty / COD Mobile whose process path is inside the install. A host that cannot enumerate windows does not count as "no window"
- GameLoop process CPU over two samples about 250 ms apart, reusing the Phase 2 monitor. A missing sample stays Unknown. System GPU is reported separately and is not attributed to the engine
- App Market version comparison from Phase 7
- Renderer from the Phase 3 configuration report
- Cache last-write time compared with the package last-write time, as an indicator
- Engine log lines when a candidate log file exists under the verified install. Matching lines are shown locally and are not uploaded

Findings are ranked. A version mismatch outranks log lines, which outrank a stopped GameLoop. Each finding names an action that already exists, or says the action is available in a later phase.

## Still out of scope

Repair, cache deletion, registry writes, and process kills. Those wait for Phase 8.

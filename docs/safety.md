# Safety boundary

GL Optimizer is a diagnostics and configuration utility.

These are out of scope for every phase:

- Injecting code into a game or into GameLoop
- Reading or writing another process's memory
- Bypassing, confusing, or disabling anti-cheat
- Spoofing hardware identifiers
- Patching game binaries or GameLoop binaries
- Forging GameLoop or App Market server responses
- Inventing FPS, temperatures, "optimization scores", install paths, or version strings

Phase 1 adds read-only detection:

- Hardware queries use WMI and the Windows version registry key. A failed query leaves that field empty.
- GameLoop detection reads uninstall metadata, the `HKLM` and `HKCU` `SOFTWARE\Tencent\GameLoop` keys in both registry views, known launcher file names, small version files, and process names. A process path that cannot be read does not drop that process. It does not write those files or those keys. `HKLM` is never written. The only registry write remains the HKCU Run value `GL Optimizer`.
- PUBG Mobile and COD Mobile are reported only from package folders found under a verified GameLoop install or, once an install exists, under the local Tencent and Documents data folders. An unfinished search is Unknown, not Not found.
- **Start** calls `Process.Start` on a launcher path that `InstallPathRules` has placed inside the verified install root. It does not start `aow_exe.exe`.
- **Close** and **Restart** return not implemented. This phase does not call `Process.Kill` or any other force-close.

Phase 2 adds a timer for CPU, memory, disk, GPU (when Windows exposes the counters), and GameLoop process CPU and memory. The timer runs only while the dashboard or Monitoring page is sampling. It does not inject, read game memory, or invent FPS. The frame provider still returns no sample, and the UI says FPS monitoring is unavailable with the current safe method.

Phase 3 reads known GameLoop config files and `HKCU\Software\Tencent\MobileGamePC` (read-only). It records path, size, and last-write time, and maps a setting only when the key and the value are both recognized. Discovery itself does not write those files or registry values.

Phase 4 copies files that discovery marked Found into `%LocalAppData%\GLOptimizer\Backups`. Restore can replace those same paths after a dry run and a confirmation dialog. It refuses path traversal, symlinks and other reparse points, paths outside the verified install and known user config locations, a backup copy whose hash does not match the manifest, and a restore while GameLoop is running. A pre-restore backup is taken before an overwrite. Registry values are stored as text and are not written back. Delete removes only a folder directly under the Backups root.

Phase 5 may replace a value only when that key was already found in a parsed, non-unreadable file and the current value is valid. It does not add keys, create files, or write the registry. It refuses to write while GameLoop is running. It creates a Phase 4 backup first and restores that backup if the read-back does not match. Graphics preference, process priority, power mode, background apps, and benchmark mode are not changed.

Phase 6 reads COD Mobile package folders, process paths, window titles on Windows, a short CPU sample, the Phase 3 renderer, and engine log lines under the verified install. It does not upload logs. **Open in GameLoop**, **Restart Engine**, and **Repair Market** do not run.

Phase 7 reads App Market paths under a verified install and may open a SQLite file with `Mode=ReadOnly`. It does not delete cache, write metadata, or repair the market. **CHECK AGAIN** does not use the network. **Check Version** calls `IOfficialVersionSource`, which currently contacts no host. **DRY RUN REPAIR** and **REPAIR APP MARKET** do not run. **Close** and **Restart** still do not kill a process.

Phase 8 repairs the local App Market only by moving cache that the Phase 7 inventory classified as Cache, after a dry run and confirmation. Each path is checked again immediately before the move: it must still be cache, still sit under the verified install, and must not be a reparse point. Metadata is copied into the backup and left in place. Package directories, game data, APKs, OBBs, key maps, user config, and unknown items are never removed. The eligible set is refused when it is larger than 5 GB, contains more than 20,000 files, or includes anything unexpected. "Clear" moves the file into `%LocalAppData%\GLOptimizer\Backups\<id>\quarantine`. A cross-volume move copies, checks the hash, and only then deletes the source. A failure restores files already moved. Restore from the Backups page puts that quarantine back and refuses to run while GameLoop is running.

**Close**, **Restart**, and the repair stop only processes whose executable path is inside the verified install. The first request asks the window to close. Force termination requires a second confirmation and a second path check. Unrelated processes are not touched. GameLoop is not started unless the user chooses **Start GameLoop**.

The pre-repair market version is saved in `repair-checkpoint.json`. **RE-CHECK AFTER GAMELOOP REFRESH** reads the local market again. If that version matches the install or is newer than the saved version, the result is a local refresh. If the repair finished, GameLoop has been reopened, and the market version is still the same outdated version, the status is REMOTE CATALOG ISSUE: "Server-side GameLoop catalog issue detected. This cannot safely be modified locally." The repair is not reported as successful when a step fails, and a remote update is never invented. `CatalogComparisonLogic` still does not return that status on its own. Official version lookup is unchanged and still contacts no host.

Developer mode is a debug-build switch. It can write a local debug log line and, only in a Debug build, substitute a simulated GameLoop scan when that setting is on. Release builds compile the simulation out and clear the flag when settings are normalized.

Phase 9 reads PUBG Mobile the same way Phase 6 reads COD Mobile: package folders, one unambiguous version, App Market metadata already on disk, a process or window under the verified install, a short CPU sample, and local engine log lines. It does not contact a host for an official PUBG version and it does not write package files.

The spec-gap pass adds one registry write: the HKCU Run value named `GL Optimizer`, created or removed only when the user saves Start with Windows. Launch Optimized sets `AboveNormal` on verified GameLoop processes and never `Realtime`. The previous priority is stored in `launch-optimized.json` and restored on request, or the journal is cleared after those processes exit. Power plan and graphics preference stay recommendations. Network diagnostics open a TCP connection only to `one.one.one.one` and `dns.google` on port 443, and only after the user presses Run. The update check in this build makes no request. Diagnostic export replaces the user-profile path with `%USERPROFILE%`.

The installer elevates to copy files into Program Files. The installed executable stays `asInvoker`. Uninstall deletes the program directory, removes the HKCU Run value `GL Optimizer` when it exists, and deletes `%LocalAppData%\GLOptimizer` only after the user agrees. The default is to keep that folder. It does not delete any other path.

An interrupted repair rolls back only files under the quarantine folder whose relative path is inside a saved install root. An interrupted optimization restores the pre-apply backup through the existing restore path. Neither recovery writes outside those backups.

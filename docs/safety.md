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
- GameLoop detection reads uninstall metadata, looks for known launcher file names, reads small version files, and reads process executable paths. It does not write those files.
- PUBG Mobile and COD Mobile are reported only from package folders found under a verified GameLoop install or, once an install exists, under the local Tencent and Documents data folders. An unfinished search is Unknown, not Not found.
- **Start** calls `Process.Start` on a launcher path that `InstallPathRules` has placed inside the verified install root. It does not start `aow_exe.exe`.
- **Close** and **Restart** return not implemented. This phase does not call `Process.Kill` or any other force-close.

Phase 2 adds a timer for CPU, memory, disk, GPU (when Windows exposes the counters), and GameLoop process CPU and memory. The timer runs only while the dashboard or Monitoring page is sampling. It does not inject, read game memory, or invent FPS. The frame provider still returns no sample, and the UI says FPS monitoring is unavailable with the current safe method.

Phase 3 reads known GameLoop config files and `HKCU\Software\Tencent\MobileGamePC` (read-only). It records path, size, and last-write time, and maps a setting only when the key and the value are both recognized. Discovery itself does not write those files or registry values.

Phase 4 copies files that discovery marked Found into `%LocalAppData%\GLOptimizer\Backups`. Restore can replace those same paths after a dry run and a confirmation dialog. It refuses path traversal, symlinks and other reparse points, paths outside the verified install and known user config locations, a backup copy whose hash does not match the manifest, and a restore while GameLoop is running. A pre-restore backup is taken before an overwrite. Registry values are stored as text and are not written back. Delete removes only a folder directly under the Backups root.

App Market files are still not opened. **OPTIMIZE NOW** cannot run. The next change to a setting is the Phase 5 preview, backup, apply, validate, and restore pipeline.

Developer mode is a debug-build switch. It can write a local debug log line. It cannot enable hidden actions in a Release build.

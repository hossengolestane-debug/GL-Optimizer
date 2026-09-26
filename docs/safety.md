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

App Market files are still not opened. Backup and restore are still not implemented. **OPTIMIZE NOW** cannot run. GameLoop config files are still not written.

A future backup feature must copy only files the user chose, and restore only after a confirmation. It must not silently rewrite a game install.

Developer mode is a debug-build switch. It can write a local debug log line. It cannot enable hidden actions in a Release build.

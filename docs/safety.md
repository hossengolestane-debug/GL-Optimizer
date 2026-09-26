# Safety boundary

GL Optimizer is a diagnostics and configuration utility.

These are out of scope for every phase:

- Injecting code into a game or into GameLoop
- Reading or writing another process's memory
- Bypassing, confusing, or disabling anti-cheat
- Spoofing hardware identifiers
- Patching game binaries or GameLoop binaries
- Forging GameLoop or App Market server responses
- Inventing FPS, temperatures, "optimization scores", or install paths

Phase 0 adds a further limit: it does not open GameLoop or App Market files at all. Detection and diagnostics interfaces exist so later read-only work has a place to land. Their current implementations return not implemented and perform no I/O.

A future backup feature must copy only files the user chose, and restore only after a confirmation. It must not silently rewrite a game install.

Developer mode is a debug-build switch. It can write a local debug log line. It cannot enable hidden actions in a Release build.

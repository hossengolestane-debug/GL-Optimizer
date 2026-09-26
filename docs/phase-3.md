# Phase 3 — Configuration discovery (read-only)

Phase 2 can show live CPU, memory, disk, and GameLoop process samples. Phase 3 should find GameLoop configuration without changing it.

## GameLoopConfigDiscovery

1. Start from install paths Phase 1 already verified. Do not scan the whole disk.
2. Look for known config, engine, and key-map files under those roots. Record the path, whether the file exists, and its size or last-write time.
3. When a file is text and small enough to read safely, report the keys that are present. Do not report values that look like tokens or account data.
4. If a file is missing, unreadable, or not a format we can parse with confidence, say Unknown. Do not fill in a recommended value.
5. Do not write, rename, or back up those files in this phase. Backup stays a later, explicit action.

The UI can list what was found on the GameLoop and Diagnostics pages. **OPTIMIZE NOW** stays disabled. Nothing in Phase 3 applies a tweak, changes priority, or edits the registry for performance.

App Market repair is still later. It is not part of Phase 3.

Close and Restart stay unimplemented until a confirmed design targets only processes inside the verified install.

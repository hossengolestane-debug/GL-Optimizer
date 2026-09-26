# Phase 4 — Backup and restore

Phase 3 can list GameLoop configuration without changing it. Phase 4 should copy and restore only those files, and only when the user asks.

## Backup

1. The chooser starts from files Phase 3 marked Found. Do not scan the disk for more paths.
2. Copy the selected files into `%LocalAppData%\GLOptimizer\Backups\<id>\`. Write a manifest with the source path, size, last-write time, and a hash. Leave the source files in place.
3. A registry snapshot may store the known values as text in the manifest. Do not import a `.reg` file and do not write registry values in this phase.

## Restore

1. Restore one manifest at a time, after an explicit confirmation that names the files.
2. Replace a live file only when the backup hash still matches the copy. If the live file changed since the backup, stop and say so.
3. Do not delete an install, do not touch files that were not in the manifest, and do not modify App Market.

**OPTIMIZE NOW** stays disabled. Close and Restart stay unimplemented until a confirmed design targets only processes inside the verified install.

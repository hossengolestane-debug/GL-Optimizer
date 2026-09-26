# Phase 4 — Backup and restore

Phase 4 copies GameLoop configuration that Phase 3 already marked Found, and can put those copies back only after a dry run and an explicit confirmation.

## Backup

1. The file list comes from `IBackupSource`. On Windows that source is the Phase 3 discovery report for verified installs, plus known Tencent user config paths. The backup does not scan the disk for extra paths.
2. Copies go to `%LocalAppData%\GLOptimizer\Backups\<BackupId>\` with `manifest.json`. The original files stay in place.
3. The manifest stores the backup id, UTC time, local display time, GameLoop version, reason, SHA-256, size, last-write time, the original absolute path, the stored file name, a `GameLoopSettings` snapshot, and registry values as text.
4. Registry text is never written back in this phase.

## Restore

1. Dry run lists each file as replace, unchanged, or skip, with the current hash and the backup hash.
2. A target is written only when it is the path recorded in the manifest and that path is inside a verified install or a known user config directory.
3. Traversal, a stored name that leaves the backup folder, and a symlink, junction, or other reparse point are skipped. If any file is skipped, the restore does not write the others.
4. The backup copy is hashed again and must match the manifest.
5. When a live file would be overwritten, a pre-restore backup of the current discovered files is created first. That backup can be restored to undo the change.
6. GameLoop running refuses the restore. This build does not stop the process.
7. Writes use a temp file in the target folder, then `File.Replace` when the platform supports it.
8. The Backups page asks for confirmation before restore and before delete. Delete removes only a direct child of the Backups folder, and it will not follow a link.

A missing or unreadable manifest is listed as damaged. It is not restored and it does not throw.

**OPTIMIZE NOW** stays disabled. App Market files are not opened. Close and Restart stay unimplemented.

## Phase 5

The next phase is the optimization engine: preview a change, back it up with this service, apply it, validate the result, and restore if validation fails. See [docs/phase-5.md](docs/phase-5.md).

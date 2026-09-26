# Phase 5 — Optimization engine

Phase 4 can copy and restore discovered configuration. Phase 5 is the first phase that may change a setting, and only through a preview, a backup, an apply, a validation, and a restore.

## Pipeline

1. **Preview.** Show the setting, the current value, and the proposed value. Do not write.
2. **Backup.** Create a Phase 4 backup of the files that would change, with a reason that names the action. If the backup fails, stop.
3. **Apply.** Write only the previewed keys in the previewed files, after confirmation. Do not touch files that were not in the preview.
4. **Validate.** Read the files back and check that the written values match the preview. Check that GameLoop still resolves to the same verified install.
5. **Restore.** If validation fails, restore the pre-apply backup and say so. The user can also restore that backup from the Backups page.

## Still out of scope

- **OPTIMIZE NOW** does not become a silent one-click rewrite. It starts this pipeline, or it stays disabled until the pipeline exists.
- No App Market repair.
- No registry writes until a later phase names the exact values and the same preview, backup, apply, validate, and restore steps.
- No process kill. Close and Restart stay unimplemented.
- No injection, memory writes, or binary patches.

The path rules from Phase 4 still apply: allowlisted paths only, no traversal, no reparse points.

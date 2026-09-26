# Phase 5 — Optimization engine

Phase 5 is the first phase that writes GameLoop configuration. A profile selection does not write. **OPTIMIZE NOW** runs the pipeline below and asks for confirmation first.

## Pipeline

1. **Analyze.** Read the hardware report and the Phase 3 configuration report.
2. **Detect.** Classify the machine as low, mid, high, or Unknown. Unknown means logical cores or total RAM were not reported.
3. **Recommend.** Build one row per setting: applicable, already optimal, skipped, not supported, or not implemented.
4. **Preview.** List each file, key, current raw value, and new raw value. Nothing is written.
5. **Backup.** Create a Phase 4 backup. If it fails, stop.
6. **Apply.** Replace only the previewed values in files Phase 3 already parsed.
7. **Validate.** Re-read the files and re-run discovery. Each changed key must read back the new value. Untouched keys must be unchanged. The install must still verify.
8. **Restore.** If validation fails, restore the pre-apply backup and report the failure.
9. **Report.** "N changes applied, N already optimal, N skipped. Restart GameLoop required." The result text is: "GameLoop configuration updated. Monitor performance during your next session to compare results."

## What may be written

A setting is written only when discovery marked the file Found, the file parsed, and the current value is valid. The editor changes that value and leaves encoding, line endings, comments, order, and every other key as they were. Supported formats are ini (including cfg and txt), json, and xml.

The editor does not add keys, does not create files, and does not write the registry. A setting that exists only in `HKCU\Software\Tencent\MobileGamePC` is shown as Not supported. GameLoop must be closed. Paths use the same allowlist and reparse checks as restore. Writes use a temp file and `File.Replace` when the platform supports it.

CPU recommendations stay at or below half of the logical processors and never use every processor. Memory recommendations stay at or below half of RAM and leave at least 4 GB for Windows. Renderer changes only step `OpenGL+` or `DirectX+` back to the base mode and are labeled compatibility-based.

## Detect only

Graphics preference, process priority, power mode, and background apps are listed as Not implemented. This phase does not change them.

Benchmark mode has an interface and returns Not implemented.

## Undo

The last successful optimization stores its backup id. Undo restores that backup after confirmation. It also refuses while GameLoop is running.

## Phase 6

The next phase is read-only COD Mobile diagnostics. See [docs/phase-6.md](docs/phase-6.md).

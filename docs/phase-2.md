# Phase 2

Phase 1 can name the PC and find GameLoop. Phase 2 should add the next read-only and explicit-confirmation features. It should not enable **OPTIMIZE NOW**.

## Suggested order

1. **App Market, still read-only.** Inspect the local App Market state only after the GameLoop install path is one Phase 1 already verified. Report what the files say. Do not download, repair, or rewrite them.
2. **Backups, explicit.** Let the user pick files, copy them under `%LocalAppData%\GLOptimizer\Backups`, and list those copies. Restore stays behind a confirmation and does not run in the background.
3. **Frame metrics without injection.** Prefer a provider that does not attach to a game or emulator process. If a number cannot be collected that way, leave the chart empty and the frame card not implemented.
4. **Version comparison, only with two real strings.** A mismatch badge is allowed only when both the installed version and the compared version were read. Do not ship a hardcoded "latest" version.
5. **Windows UI pass.** Exercise the custom chrome on Windows 10 and 11: snap layout, 100% / 150% / 200% DPI, a 1100×700 window, and high contrast. Fix what that pass finds.
6. **Packaging.** MSIX or WiX, x64, .NET 8 Desktop Runtime prerequisite or a self-contained publish. The package installs GL Optimizer only.

Close and Restart stay unimplemented until a design requires an explicit confirmation and only targets processes whose executable path is inside the verified install. Do not force-kill as a side effect of a scan.

Do not wire **OPTIMIZE NOW** to process priority, registry changes, or GameLoop config edits until each action has its own reviewed design.

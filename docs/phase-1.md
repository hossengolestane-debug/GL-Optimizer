# Phase 1

Phase 0 shipped the shell, settings, logging, navigation, and honest stubs. Phase 1 should add real read-only information and stop there.

## Suggested order

1. **GameLoop detection, read-only.** Resolve an install location from documented or user-selected paths. Do not write config files. If nothing is found, show that. Do not guess a version string.
2. **Hardware report, read-only.** Query CPU, GPU, and memory through documented Windows APIs. Leave a field blank when the API does not return it.
3. **Diagnostics page.** Keep the local path checks. Add the real hardware and GameLoop rows only after those services stop returning not implemented.
4. **Backups, explicit.** Let the user pick files, copy them under `%LocalAppData%\GLOptimizer\Backups`, and list those copies. Restore stays behind a confirmation and still does not run in the background.
5. **Monitoring spike.** Prefer providers that do not inject into a process. If that cannot be done safely, leave frame metrics unimplemented.
6. **Packaging.** MSIX or WiX, x64, .NET 8 Desktop Runtime prerequisite or a self-contained publish. Code signing is separate.
7. **Windows UI pass.** Snap the custom chrome, check 100% / 150% / 200% DPI, and check a narrow 1100×700 window.

Do not wire **OPTIMIZE NOW** to process priority, registry "tweaks", or GameLoop config edits until each action has its own reviewed design.

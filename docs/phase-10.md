# Phase 10 — Windows installer

The installer is an Inno Setup 6 script, `installer/GLOptimizer.iss`. It is compiled on Windows. The installed application stays `asInvoker`. Only the setup elevates.

## What it installs

- Privileges: `PrivilegesRequired=admin`
- Directory: `{autopf}\GL Optimizer` (Program Files on a 64-bit machine)
- Architecture: `x64compatible`, installed in 64-bit mode
- AppId: `{8F4E2A1C-6B3D-4E90-9C1A-7D5E2F8A4B16}` so an upgrade replaces the same install
- CloseApplications filter `GLOptimizer.exe`, plus the `GLOptimizer` mutex, so a running copy is closed or the user is asked
- Start Menu shortcut. Desktop shortcut is an unchecked task
- Uninstall entry uses the executable icon, the build version, and publisher GL Optimizer
- Version comes from `ISCC /DMyAppVersion=` (the workflow reads `Directory.Build.props`)

The payload is the self-contained win-x64 folder publish at `publish\`. WPF is published as a folder, not a single file, because a single-file WPF host is unreliable with native and XAML assets.

## Uninstall

Program files under the install directory are removed. If `%LocalAppData%\GLOptimizer` exists, setup asks whether to remove backups, logs, and settings. The default button is No, so a silent uninstall keeps that folder. The HKCU Run value named `GL Optimizer` is removed when it is present. Nothing outside those two locations is deleted.

## Workflow

`.github/workflows/windows-installer.yml` runs on `push`, `pull_request`, and `workflow_dispatch`. On `windows-latest` it builds the solution, runs the tests, publishes `publish\GLOptimizer.exe`, compiles `GL-Optimizer-Setup.exe`, and smoke-tests a silent install, `--smoke-test`, and a silent uninstall. Artifacts: `GL-Optimizer-Setup` and `publish`.

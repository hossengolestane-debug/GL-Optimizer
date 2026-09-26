# Phase 10 — Windows installer

Phase 10 is not implemented in this environment. The installer has to be built on Windows.

## Workflow

`.github/workflows/windows-installer.yml` is `workflow_dispatch` only, so it does not run on this pull request. On `windows-latest` it:

1. Publishes a self-contained `win-x64` build of `GLOptimizer`.
2. Compiles `installer/GLOptimizer.iss` with Inno Setup.
3. Uploads `GL-Optimizer-Setup.exe` as an artifact.

The package is per-user (`PrivilegesRequired=lowest`). It installs only the published GL Optimizer files. It does not include GameLoop, App Market payloads, or a request for administrator rights at install time.

## Why it is not built here

This agent runs on Linux. Inno Setup and a Windows publish that is then executed are not verified on this host. Dispatch the workflow from GitHub Actions when a Windows artifact is needed.

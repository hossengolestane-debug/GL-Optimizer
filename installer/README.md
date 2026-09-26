# Installer

Phase 10 builds the installer on Windows. This repository includes the Inno Setup script and a `workflow_dispatch` GitHub Actions workflow. Neither runs on the Linux test host.

On `windows-latest`, `.github/workflows/windows-installer.yml` publishes a self-contained `win-x64` build and compiles `GLOptimizer.iss` into `GL-Optimizer-Setup.exe`.

The package is per-user, requests no elevation, and installs only the GL Optimizer binaries. It does not include GameLoop files or App Market payloads.

A framework-dependent publish, without the installer, is still:

```powershell
.\scripts\publish-windows.ps1
```

# Installer

Phase 1 does not ship an installer.

On Windows, publish a framework-dependent x64 build with:

```powershell
.\scripts\publish-windows.ps1
```

That produces `GLOptimizer.exe` plus its dependencies. The machine needs the .NET 8 Desktop Runtime.

A later phase can wrap that output in MSIX or WiX. The package should stay per-user, request no elevation, and install only the GL Optimizer binaries. It must not include GameLoop files or App Market payloads.

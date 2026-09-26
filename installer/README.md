# Installer

`GLOptimizer.iss` is an Inno Setup 6 script. It installs under Program Files (`{autopf}\GL Optimizer`) and requires an administrator for setup. The installed `GLOptimizer.exe` stays `asInvoker`.

Publish a self-contained win-x64 folder to `publish\` first (single-file publish is not used). Then:

```powershell
& "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe" "/DMyAppVersion=0.1.0" installer\GLOptimizer.iss
```

`MyAppVersion` must be three numeric parts so `VersionInfoVersion` can append `.0`. Output: `installer\output\GL-Optimizer-Setup.exe`.

Uninstall removes the program directory. It asks before deleting `%LocalAppData%\GLOptimizer`. The default is to keep that folder, including when the uninstaller is silent. It also removes the HKCU Run value `GL Optimizer` if that value exists.

`.github/workflows/windows-installer.yml` builds, tests, publishes, compiles, and smoke-tests this package on `windows-latest`.

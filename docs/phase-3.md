# Phase 3 — Configuration discovery (read-only)

Phase 3 reads GameLoop configuration. It does not write, rename, back up, or repair anything.

## Where it looks

Discovery starts from install paths Phase 1 already verified. If none were verified, it does not search. It does not walk the disk. It checks a fixed list:

- Under the install: `ui/DefaultKeyMapping.xml`, `ui/config.ini`, `ui/UserConfig.ini`, `config.ini`, and `app.ini` (plus the `UI` casing of the same relative paths)
- Known user files, only after an install exists: `config.ini`, `UserConfig.ini`, `ui_config.json`, and `config.json` under `Tencent\MobileGamePC` and `Tencent\GameLoop` in both `%AppData%` and `%LocalAppData%`
- `%AppData%\AndroidTbox\TVM_100.xml` (key map)
- `HKCU\Software\Tencent\MobileGamePC`, read with `OpenSubKey(..., writable: false)`

Text files larger than 64 KB, links, and documents that are not valid ini, json, or xml are recorded as Unreadable and are not parsed. Key maps are recorded with path, size, and last-write time. Their contents are not turned into engine settings.

## Confidence

`GameLoopSettings` has Renderer, Resolution, DPI, MemoryAllocation, CpuAllocation, VSync, AntiAliasing, and FpsTarget. A field stays null unless every accepted value for it agrees.

Mapped keys, from public GameLoop and Tencent Gaming Buddy registry exports:

- `VMResWidth` and `VMResHeight`, or a `Resolution` value such as `1280x720`, when both sides are from 320 to 8192
- `VMDPI` or `DPI`, from 80 to 960
- `VMMemorySizeInMB` or `MemoryAllocation`, from 128 to 131072, shown as megabytes
- `VMCpuCount` or `CpuAllocation`, from 1 to 64. Zero stays Unknown
- `VSyncEnabled` or `VSync`: 0/1, true/false, on/off
- `FxaaQuality` or `AntiAliasing`: the words Off, Balanced, or Ultra, or a raw integer 0–8. The integer is not translated to a name
- `Renderer`, `RendererMode`, or `ScreenRenderingMode` only when the value is OpenGL+, OpenGL, DirectX+, DirectX, or Auto
- `FPS`, `FPSLevel`, `FpsTarget`, or a package key ending in `_FPSLevel`, from 15 to 240. Two different numbers stay Unknown

`ForceDirectX`, `EnableGLESv3`, and `RendererEngine` are reported as present and are not mapped to a renderer name. The published numeric mapping to OpenGL+, DirectX+, and Auto is not consistent. Account-like values (`@`, paths, long hex strings) are ignored. Unknown keys are ignored.

With one verified install, user files and the registry key are merged into that install. If two values differ, that setting stays Unknown. With more than one install, those user-level values stay in a separate section and are not copied onto every install.

## UI

The GameLoop page lists the eight settings and every checked location as Found, Not found, or Unreadable, with size and last-write time when a file was present. Diagnostics repeats the settings for the first install and lists locations that were found or unreadable. The scan is async and cancellable. **OPTIMIZE NOW** stays disabled. Close and Restart stay unimplemented.

## Limits

Linux does not read the registry. A missing counter or a missing key stays Unknown. The registry hive is per Windows user, not a file inside the install folder, so a single verified install is the reason those values are shown for it. Phase 1 can still leave total VRAM Unknown for large adapters. This phase does not open App Market files.

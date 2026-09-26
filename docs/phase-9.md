# Phase 9 — PUBG Mobile and the spec-gap pass

Phase 9 is implemented. PUBG Mobile diagnostics are read-only. The spec-gap pass adds the local preferences, tray, first-run summary, Launch Optimized priority session, diagnostic export, activity timeline, toasts, update-check architecture, and user-triggered network check that the earlier phases had not built.

## PUBG Mobile

- Package ids stay the known `MobilePackages.Pubg` list.
- The installed version comes from one unambiguous local version file. A missing or conflicting value stays Unknown.
- The market version is read from App Market metadata the Phase 7 scan already produced. There is no new catalog endpoint and no unofficial scrape.
- The official version stays Unknown. `IOfficialVersionSource` is not called, and no host is contacted.
- Launch status uses a process under the verified install, or a window whose process is also under that install. A title that only says PUBG is not enough.
- A short GameLoop CPU sample and local engine log lines are included. Logs are not uploaded. User-profile paths in those lines are replaced with `%USERPROFILE%`.
- The optimization line reuses the Phase 5 engine. No PUBG-specific file or registry value is written.
- The PUBG page, the Diagnostics section, and the dashboard card show that result. Unknown stays Unknown.

## Spec-gap pass

- Settings: Start with Windows writes or removes only the HKCU Run value named `GL Optimizer`, and only when the user saves. Minimize to tray, sampling interval, automatic backup, update check, a GameLoop path override that must contain a real launcher, log level, and theme. Dark is applied. Light can be stored and is reported as not implemented.
- Tray: Open GL Optimizer, Launch GameLoop, Launch Optimized, Monitoring, and Exit. Monitoring off stops sampling.
- First run: “Welcome to GL Optimizer / Scanning your system...” then a short hardware, GameLoop, games, virtualization, and recommendation summary. It is shown once.
- Launch Optimized sets AboveNormal on verified GameLoop processes. Realtime is never set. The journal is cleared when those processes exit. If the app stops first, the next startup offers restore or dismiss. Power plan and graphics preference stay Not Implemented.
- Diagnostics can export TXT and JSON and copy the report. User-profile paths become `%USERPROFILE%`.
- Activity is a timeline of the app log with a details pane.
- Toasts are the five catalog messages, and a repeat inside two minutes is dropped.
- `IUpdateService` validates HTTPS, a numeric version, and a SHA-256. The development build returns Not Implemented and makes no request.
- Network diagnostics run only after Run, and only against `one.one.one.one` and `dns.google` on port 443. Traffic is not changed.
- The mark is an original monochrome geometric G and L.
- The process stays asInvoker. A failed priority change explains elevation and can relaunch only `launch-optimized` or `restore-priority`.
- Developer simulation is compiled out of Release, and settings normalization clears the flag there.

## Still not implemented

- Light theme.
- Power plan changes and graphics preference changes.
- Downloading or applying a GL Optimizer update.
- An official PUBG or COD version source.
- FPS and benchmark mode.
- The Windows installer. That is Phase 10.

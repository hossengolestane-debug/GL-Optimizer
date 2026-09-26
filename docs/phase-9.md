# Phase 9 — PUBG Mobile diagnostics plan

Phase 9 is not implemented. PUBG Mobile is still the Phase 1 presence check: Installed, Not found, or Unknown, from package folders under a verified GameLoop install or the known Tencent and Documents locations. No version is invented.

## Intended scope

Mirror the read-only COD Mobile diagnostics for PUBG Mobile:

- Detect the known PUBG package ids already listed in `MobilePackages.Pubg`.
- Read a version only from an unambiguous local version file. A missing or ambiguous value stays Unknown.
- Compare that version with App Market metadata already found by Phase 7. Do not add a new catalog endpoint and do not scrape an unofficial host.
- Collect the same kind of local evidence used for COD: a process or window under the verified install, a short CPU sample, and engine log lines that were already on disk. Do not upload logs.
- Show the result on the PUBG Mobile page and as a Diagnostics row. Unknown stays Unknown.

## Out of scope

- App Market repair changes. Phase 8 already covers cache quarantine, and a PUBG package directory is not cache.
- Stopping PUBG by itself. GameLoop Close and Restart stay limited to verified GameLoop processes.
- Writing config, registry values, or package files.
- An official PUBG version source until a stable public endpoint is identified. Until then the official version stays Unknown and no host is contacted.

# Phase 6 — COD Mobile diagnostics

Phase 5 can change GameLoop configuration through a confirmed pipeline. Phase 6 stays on the COD Mobile page and is read-only.

## Scope

1. Report whether COD Mobile is installed from the package folders Phase 1 already searches under a verified GameLoop install.
2. Show the version only when a local version file is unambiguous. Otherwise show Unknown.
3. List the package path, size, and last-write time when the folder is present. Do not parse game binaries.
4. If a known COD Mobile config file is documented and sits inside the verified install, record its path and whether it is readable. Do not modify it.
5. Keep PUBG Mobile on the same read-only presence rules.

## Still out of scope

- No App Market repair.
- No registry writes.
- No process kill. Close and Restart stay unimplemented.
- No injection, memory reads, or binary patches.
- Optimization of a COD Mobile setting waits until the file and the keys are named the same way Phase 5 names GameLoop keys: found, parsed, previewed, backed up, validated, and reversible.

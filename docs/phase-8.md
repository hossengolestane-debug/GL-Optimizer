# Phase 8 — App Market repair plan

Phase 8 is not implemented. Phases 6 and 7 only read. A repair, when it is built, stays inside the verified install and only touches cache paths that the Phase 7 inventory classified and that an allowlist names.

## Steps

1. **Dry run.** Show the allowlisted cache paths, sizes, and last-write times. Change nothing. This is the action behind the disabled DRY RUN REPAIR button.
2. **Backup.** Copy those files with the Phase 4 backup service before any delete. Refuse the repair when GameLoop is running, when a path is a reparse point, or when a path is outside the verified install.
3. **Repair.** Delete or clear only the allowlisted cache entries from that dry run. Do not write configuration, registry values, package files, or metadata databases.
4. **Refresh.** Read the local market version again from the same metadata rules as Phase 7.
5. **Restart.** Offer a restart only as a confirmed, separate action. This phase must not kill a process as a side effect of repair.
6. **Validate.** Compare installed, market, and official versions again.
7. **Local versus remote.** REMOTE CATALOG ISSUE is allowed only when the allowlisted cache was repaired, the local metadata was read again, and the market version is still older or still mismatched in the way a local cache cannot explain. The message is fixed: "Server-side GameLoop catalog issue detected. This cannot safely be modified locally." Until that evidence exists, the comparison stays "Cannot yet distinguish local vs remote."

Official version lookup still goes through `IOfficialVersionSource`. Phase 8 does not add an unofficial mirror and does not invent a version.

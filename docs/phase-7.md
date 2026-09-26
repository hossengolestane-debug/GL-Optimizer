# Phase 7 — App Market diagnostics

Phase 7 reads App Market files under a verified GameLoop install. It does not delete, clear, or repair them.

## Inventory

`AppMarketDetector` walks only a verified install root. The walk stops at depth 6 and 400 entries, skips reparse points, and honors cancellation. Each kept path records size, file count, and last-write time.

Classification uses names already in the launcher and package lists. The directory layout is an assumption until it is checked on a real install:

| Evidence | Kind | Confidence |
| --- | --- | --- |
| `AppMarket.exe` | Package | High |
| Directory named as a known COD Mobile package id | Package | High |
| Version file inside that package directory | Metadata | High |
| `AppMarket` path with a `cache` or `caches` directory | Cache | Medium |
| `.db`, `.sqlite`, `.json`, `.xml`, or `.ini` under `AppMarket` | Metadata | Medium |
| Other `AppMarket` path | Unknown | Low |

`AppMarketCacheManager` only filters that inventory to cache entries. It has no delete or clear method.

## Versions

The installed version is read again with `PackageVersion`, so a prerelease string is not turned into a mismatch. The local market version is read only from metadata under an `AppMarket` path, including a SQLite file opened with `Mode=ReadOnly` when a version column is unambiguous. Zero matches or several different versions stay Unknown.

`IOfficialVersionSource` is implemented by `UnavailableOfficialVersionSource`. No official GameLoop or Tencent HTTPS catalog was verified, so the result is Unknown and the detail is "Official version source not implemented." The call does not open a connection. **Check Version** is the only control that invokes it, and the log records the host when one is contacted. **CHECK AGAIN** on the App Market page scans locally and does not use the network.

## Comparison

| State | When |
| --- | --- |
| MATCH | Installed and market versions parse and are equal. Official is missing or equal. |
| VERSION MISMATCH | Installed and market versions both parse and differ. |
| LOCAL MARKET OUTDATED | Installed and market match, and the official version is newer than the market. |
| UNKNOWN | A local version is missing or not unambiguous, or the official version is older than the market. The detail in that last case is "Cannot yet distinguish local vs remote." |
| REMOTE CATALOG ISSUE | Not returned. Phase 8 may use it only after a repair still shows the same mismatch. The message, when that happens, is "Server-side GameLoop catalog issue detected. This cannot safely be modified locally." |

The App Market page shows status, the three versions, the last scan, and the detected issue. **DRY RUN REPAIR** and **REPAIR APP MARKET** are visible and disabled, with the reason "Available in Phase 8." **What will be changed?** lists the cache inventory a later repair would be limited to. Nothing is changed now.

The Diagnostics page adds APP MARKET and COD MOBILE rows with PASS, WARNING, FAILED, or UNKNOWN. The dashboard COD Mobile card uses this comparison. Unknown does not show a mismatch.

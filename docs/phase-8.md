# Phase 8 — App Market repair

Phase 8 repairs the local App Market by moving cache into a backup quarantine. It does not delete those files, and it does not change GameLoop's remote catalog.

## Eligibility

Only an inventory item classified **Cache** by the Phase 7 rules is eligible, and only when it is inside a verified GameLoop install. A Tencent user-cache directory is eligible only when that same classification calls it App Market cache. The path is checked again immediately before anything is moved.

These are never removed:

- metadata databases (they may be copied into the backup)
- package directories
- game data
- APK, OBB, and XAPK files
- user configuration and key maps
- anything classified Unknown
- reparse points

The repair stops for review when the eligible set is larger than 5 GB, contains more than 20,000 files, or includes a path that is not safe to clear. Nothing is moved in that case.

## Steps

1. **Dry run.** **DRY RUN REPAIR** lists "Would stop", "Would back up", "Would clear", and "Would restart", and states that no game data will be removed. **What will be changed?** lists those paths. Nothing is changed.
2. **Confirm.** **REPAIR APP MARKET** asks before it starts. A COD Mobile or PUBG Mobile session warning is shown when a matching process or window is visible.
3. **Stop GameLoop.** Only GameLoop, AppMarket, TxGameAssistant, AndroidEmulator, AndroidEmulatorEn, and aow_exe processes whose executable path is inside the verified install are asked to close. The request is graceful. Force termination requires a second confirmation and a second path check. Other processes are not touched. The same rules apply to **Close** and **Restart** on the GameLoop page.
4. **Back up.** Metadata files are copied into `%LocalAppData%\GLOptimizer\Backups\<id>\metadata` and left in place. The manifest records original path, size, and hash.
5. **Clear.** Eligible cache files are moved into `%LocalAppData%\GLOptimizer\Backups\<id>\quarantine`. A move onto another volume copies the file, checks the SHA-256, and only then deletes the source. Cancel is honored until the first successful move. After that, the repair finishes or puts already-moved files back.
6. **Restart.** **START GAMELOOP** is offered and launches only a verified launcher. It is not started automatically.
7. **Re-check.** The pre-repair market version is stored in `%LocalAppData%\GLOptimizer\repair-checkpoint.json`. **RE-CHECK AFTER GAMELOOP REFRESH** reads the local market again after the user reopens GameLoop.

## Local versus remote

- If the market version after that refresh matches the installed version or is newer than the saved version: "Local App Market refreshed. COD Mobile market version is now X."
- If the local repair finished and the market still reports the same outdated version: "The local App Market has been refreshed successfully. However, the currently detected GameLoop server catalog still provides the same COD Mobile version. GL Optimizer cannot safely change GameLoop's remote catalog." Status is REMOTE CATALOG ISSUE, with "Server-side GameLoop catalog issue detected. This cannot safely be modified locally."
- A failed step does not report success and does not report a remote update. `CatalogComparisonLogic` still never returns REMOTE CATALOG ISSUE by itself.

The activity log and the toast "GameLoop App Market repair completed." are written only when the move finishes. Restoring the backup from the Backups page puts the quarantined cache back, with the same path checks, and refuses to run while GameLoop is running.

Official version lookup is unchanged. No unofficial catalog is contacted.

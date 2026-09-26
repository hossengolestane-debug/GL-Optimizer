namespace GLOptimizer.Core.Diagnostics;

public static class Phase0Notices
{
    public const string Safety =
        "GL Optimizer is a diagnostics and configuration utility. It does not inject into games, modify game memory, bypass anti-cheat, spoof hardware IDs, patch game binaries, or imitate GameLoop server responses.";

    public const string ReadOnlyGameLoop =
        "This page reads GameLoop metadata and configuration. Restore, on the Backups page, can replace those same files only after a dry run and confirmation.";

    public const string NoAppMarketIo =
        "App Market files under a verified GameLoop install can be read. This build does not delete, clear, or repair them.";

    public const string NoFrameMetrics =
        "FPS monitoring unavailable with current safe monitoring method.";

    public const string NoOptimization =
        "OPTIMIZE NOW writes only keys already found in a parsed config file, and only after a backup. Registry values are not written. Close GameLoop first.";

    public const string BackupSafety =
        "Backups copy discovered GameLoop configuration into local app data. Restore replaces only the recorded paths after a dry run and confirmation, and only when GameLoop is not running. Registry values are stored as text and are not written back.";
}

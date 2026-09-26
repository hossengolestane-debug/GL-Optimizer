namespace GLOptimizer.Core.Diagnostics;

public static class Phase0Notices
{
    public const string Safety =
        "GL Optimizer is a diagnostics and configuration utility. It does not inject into games, modify game memory, bypass anti-cheat, spoof hardware IDs, patch game binaries, or imitate GameLoop server responses.";

    public const string ReadOnlyGameLoop =
        "This page reads GameLoop metadata and configuration. Restore, on the Backups page, can replace those same files only after a dry run and confirmation.";

    public const string NoAppMarketIo =
        "This build does not read or modify App Market files.";

    public const string NoFrameMetrics =
        "FPS monitoring unavailable with current safe monitoring method.";

    public const string NoOptimization =
        "Optimization actions are not available in this phase.";

    public const string BackupSafety =
        "Backups copy discovered GameLoop configuration into local app data. Restore replaces only the recorded paths after a dry run and confirmation, and only when GameLoop is not running. Registry values are stored as text and are not written back.";
}

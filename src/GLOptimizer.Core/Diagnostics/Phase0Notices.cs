namespace GLOptimizer.Core.Diagnostics;

public static class Phase0Notices
{
    public const string Safety =
        "GL Optimizer is a diagnostics and configuration utility. It does not inject into games, modify game memory, bypass anti-cheat, spoof hardware IDs, patch game binaries, or imitate GameLoop server responses.";

    public const string NoGameLoopIo =
        "This build does not read or modify GameLoop files.";

    public const string NoAppMarketIo =
        "This build does not read or modify App Market files.";

    public const string NoLiveMetrics =
        "Hardware and frame metrics are not collected in this phase.";

    public const string NoOptimization =
        "Optimization actions are not available in this phase.";

    public const string NoBackup =
        "Backup and restore are not implemented. Nothing is copied or restored.";
}

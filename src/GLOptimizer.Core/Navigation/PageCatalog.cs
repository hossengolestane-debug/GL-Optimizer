namespace GLOptimizer.Core.Navigation;

public sealed record PageInfo(AppPage Page, string Title, string Group, string Subtitle);

public static class PageCatalog
{
    public static IReadOnlyList<PageInfo> All { get; } =
    [
        new(AppPage.Dashboard, "Dashboard", "Overview", "Status of this install. Live tuning is not active in this build."),
        new(AppPage.Optimize, "Optimize", "Performance", "Optimization actions are not available in this phase."),
        new(AppPage.Monitoring, "Monitoring", "Performance", "Hardware and frame metrics are not collected in this phase."),
        new(AppPage.GameLoop, "GameLoop", "GameLoop", "Detection is not implemented. This build does not read or modify GameLoop files."),
        new(AppPage.AppMarket, "App Market", "GameLoop", "Diagnostics are not implemented. This build does not read or modify App Market files."),
        new(AppPage.CodMobile, "COD Mobile", "Games", "No game data is read in this phase."),
        new(AppPage.PubgMobile, "PUBG Mobile", "Games", "No game data is read in this phase."),
        new(AppPage.Diagnostics, "Diagnostics", "System", "Checks for this app's own files. Game clients are not inspected."),
        new(AppPage.Backups, "Backups", "System", "Backup and restore are not implemented."),
        new(AppPage.Logs, "Logs", "System", "Entries written by this app."),
        new(AppPage.Settings, "Settings", "System", "Local preferences for GL Optimizer.")
    ];

    public static PageInfo Get(AppPage page)
    {
        foreach (var info in All)
        {
            if (info.Page == page)
            {
                return info;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(page), page, "Unknown page.");
    }
}

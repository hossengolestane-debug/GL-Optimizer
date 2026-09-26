namespace GLOptimizer.Core.Navigation;

public sealed record PageInfo(AppPage Page, string Title, string Group, string Subtitle);

public static class PageCatalog
{
    public static IReadOnlyList<PageInfo> All { get; } =
    [
        new(AppPage.Dashboard, "Dashboard", "Overview", "Read-only hardware and GameLoop status. Optimization is not active."),
        new(AppPage.Optimize, "Optimize", "Performance", "Optimization actions are not available in this phase."),
        new(AppPage.Monitoring, "Monitoring", "Performance", "Live CPU, memory, disk, and GameLoop samples. FPS is not collected."),
        new(AppPage.GameLoop, "GameLoop", "GameLoop", "Read-only detection and configuration report. Config files are not modified."),
        new(AppPage.AppMarket, "App Market", "GameLoop", "Diagnostics are not implemented. This build does not read or modify App Market files."),
        new(AppPage.CodMobile, "COD Mobile", "Games", "Install presence is read from GameLoop local data when a verified install exists."),
        new(AppPage.PubgMobile, "PUBG Mobile", "Games", "Install presence is read from GameLoop local data when a verified install exists."),
        new(AppPage.Diagnostics, "Diagnostics", "System", "Local files, hardware, and a read-only GameLoop configuration report."),
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

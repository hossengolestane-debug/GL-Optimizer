namespace GLOptimizer.Core.Navigation;

public sealed record PageInfo(AppPage Page, string Title, string Group, string Subtitle);

public static class PageCatalog
{
    public static IReadOnlyList<PageInfo> All { get; } =
    [
        new(AppPage.Dashboard, "Dashboard", "Overview", "Hardware and GameLoop status. OPTIMIZE NOW opens Optimize when a recommendation can be applied."),
        new(AppPage.Optimize, "Optimize", "Performance", "Preview a profile, then apply it to keys that were already found. Registry values are not written."),
        new(AppPage.Monitoring, "Monitoring", "Performance", "Live CPU, memory, disk, and GameLoop samples. FPS is not collected."),
        new(AppPage.GameLoop, "GameLoop", "GameLoop", "Read-only detection and configuration report. Config files are not modified."),
        new(AppPage.AppMarket, "App Market", "GameLoop", "Moves only re-validated App Market cache into a backup quarantine. Game data is not removed."),
        new(AppPage.CodMobile, "COD Mobile", "Games", "Reads the local install and compares it with App Market metadata. Check Version is the only control that asks for an official version."),
        new(AppPage.PubgMobile, "PUBG Mobile", "Games", "Install presence is read from GameLoop local data when a verified install exists."),
        new(AppPage.Diagnostics, "Diagnostics", "System", "Local files, hardware, and a read-only GameLoop configuration report."),
        new(AppPage.Backups, "Backups", "System", "Copy and restore discovered GameLoop configuration. Registry values are not written back."),
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

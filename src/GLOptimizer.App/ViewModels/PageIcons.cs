using GLOptimizer.Core.Navigation;

namespace GLOptimizer.App.ViewModels;

internal static class PageIcons
{
    public static string For(AppPage page) => page switch
    {
        AppPage.Dashboard => "\uE80F",
        AppPage.Optimize => "\uE9E9",
        AppPage.Monitoring => "\uE9D2",
        AppPage.GameLoop => "\uE895",
        AppPage.AppMarket => "\uE719",
        AppPage.CodMobile => "\uE7FC",
        AppPage.PubgMobile => "\uE7FC",
        AppPage.Diagnostics => "\uE9D9",
        AppPage.Backups => "\uE74E",
        AppPage.Logs => "\uE7C3",
        AppPage.Settings => "\uE713",
        _ => "\uE897"
    };
}

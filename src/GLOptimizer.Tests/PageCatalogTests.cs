using GLOptimizer.Core.Navigation;

namespace GLOptimizer.Tests;

public class PageCatalogTests
{
    [Fact]
    public void Every_page_is_listed_once()
    {
        var expected = Enum.GetValues<AppPage>().OrderBy(page => page).ToArray();
        var actual = PageCatalog.All.Select(info => info.Page).OrderBy(page => page).ToArray();

        Assert.Equal(expected, actual);
        Assert.Equal(expected.Length, PageCatalog.All.Select(info => info.Page).Distinct().Count());
    }

    [Fact]
    public void Navigation_ignores_a_repeated_page()
    {
        var navigation = new GLOptimizer.Infrastructure.Navigation.NavigationService();
        var changes = 0;
        navigation.CurrentChanged += (_, _) => changes++;

        navigation.Navigate(AppPage.Logs);
        navigation.Navigate(AppPage.Logs);

        Assert.Equal(AppPage.Logs, navigation.Current);
        Assert.Equal(1, changes);
    }
}

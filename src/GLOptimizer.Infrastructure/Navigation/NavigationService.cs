using GLOptimizer.Core.Navigation;

namespace GLOptimizer.Infrastructure.Navigation;

public sealed class NavigationService : INavigationService
{
    public AppPage Current { get; private set; } = AppPage.Dashboard;

    public event EventHandler<AppPage>? CurrentChanged;

    public void Navigate(AppPage page)
    {
        if (!Enum.IsDefined(page))
        {
            throw new ArgumentOutOfRangeException(nameof(page), page, "Unknown page.");
        }

        if (Current == page)
        {
            return;
        }

        Current = page;
        CurrentChanged?.Invoke(this, page);
    }
}

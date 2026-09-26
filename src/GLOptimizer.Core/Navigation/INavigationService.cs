namespace GLOptimizer.Core.Navigation;

public interface INavigationService
{
    AppPage Current { get; }

    event EventHandler<AppPage>? CurrentChanged;

    void Navigate(AppPage page);
}

using GLOptimizer.Core.Navigation;

namespace GLOptimizer.App.ViewModels;

public interface IPageViewModel
{
    AppPage Page { get; }

    string Title { get; }

    string Subtitle { get; }
}

public interface IRefreshable
{
    void Refresh();
}

public interface IPageViewModelFactory
{
    IPageViewModel Create(AppPage page);
}

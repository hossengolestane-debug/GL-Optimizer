using CommunityToolkit.Mvvm.ComponentModel;
using GLOptimizer.Core.Navigation;

namespace GLOptimizer.App.ViewModels;

public abstract partial class PageViewModel : ObservableObject, IPageViewModel
{
    protected PageViewModel(AppPage page)
    {
        Page = page;
        var info = PageCatalog.Get(page);
        Title = info.Title;
        Subtitle = info.Subtitle;
    }

    public AppPage Page { get; }

    public string Title { get; }

    public string Subtitle { get; }
}

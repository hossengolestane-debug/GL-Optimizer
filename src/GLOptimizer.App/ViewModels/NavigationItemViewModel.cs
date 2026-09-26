using CommunityToolkit.Mvvm.ComponentModel;
using GLOptimizer.Core.Navigation;

namespace GLOptimizer.App.ViewModels;

public partial class NavigationItemViewModel : ObservableObject
{
    public NavigationItemViewModel(AppPage page, string title, string group, string iconGlyph)
    {
        Page = page;
        Title = title;
        Group = group;
        IconGlyph = iconGlyph;
    }

    public AppPage Page { get; }

    public string Title { get; }

    public string Group { get; }

    public string IconGlyph { get; }

    [ObservableProperty]
    private bool _isSelected;
}

public sealed class NavigationSectionViewModel
{
    public required string Title { get; init; }

    public required IReadOnlyList<NavigationItemViewModel> Items { get; init; }
}

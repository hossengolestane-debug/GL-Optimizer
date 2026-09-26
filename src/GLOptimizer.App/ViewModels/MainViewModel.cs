using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.Core;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Navigation;

namespace GLOptimizer.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly IPageViewModelFactory _pages;
    private readonly ISettingsStore _settings;
    private readonly ILogStore _log;
    private readonly IMonitoringCoordinator _monitoring;
    private bool _persistSidebar;

    public MainViewModel(
        INavigationService navigation,
        IPageViewModelFactory pages,
        ISettingsStore settings,
        ILogStore log,
        IMonitoringCoordinator monitoring)
    {
        _navigation = navigation;
        _pages = pages;
        _settings = settings;
        _log = log;
        _monitoring = monitoring;
        Sections = PageCatalog.All
            .GroupBy(page => page.Group)
            .Select(group => new NavigationSectionViewModel
            {
                Title = group.Key,
                Items = group.Select(page => new NavigationItemViewModel(page.Page, page.Title, page.Group, PageIcons.For(page.Page))).ToList()
            })
            .ToList();
        IsSidebarCollapsed = settings.Current.SidebarCollapsed;
        _persistSidebar = true;
        Navigate(AppPage.Dashboard);
    }

    public IReadOnlyList<NavigationSectionViewModel> Sections { get; }

    public string VersionLabel => $"{BuildInfo.PhaseName}  {BuildInfo.Version}";

    public string CollapseGlyph => IsSidebarCollapsed ? "\uE76B" : "\uE76C";

    public string CollapseToolTip => IsSidebarCollapsed ? "Expand sidebar" : "Collapse sidebar";

    [ObservableProperty]
    private bool _isSidebarCollapsed;

    [ObservableProperty]
    private IPageViewModel? _currentViewModel;

    [RelayCommand]
    private void Navigate(AppPage page)
    {
        _navigation.Navigate(page);
        _monitoring.SetPage(page);
        CurrentViewModel = _pages.Create(page);
        foreach (var item in Sections.SelectMany(section => section.Items))
        {
            item.IsSelected = item.Page == page;
        }
    }

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    partial void OnIsSidebarCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(CollapseGlyph));
        OnPropertyChanged(nameof(CollapseToolTip));
        if (!_persistSidebar)
        {
            return;
        }

        var settings = _settings.Current;
        settings.SidebarCollapsed = value;
        var result = _settings.Save(settings);
        if (!result.Succeeded)
        {
            _log.Write(LogSeverity.Warning, "Settings", result.Error ?? "Could not save the sidebar state.");
        }
    }
}

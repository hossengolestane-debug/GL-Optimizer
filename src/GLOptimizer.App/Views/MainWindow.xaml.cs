using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using GLOptimizer.App.ViewModels;

namespace GLOptimizer.App.Views;

public partial class MainWindow : Window
{
    private const double ExpandedWidth = 240;
    private const double CollapsedWidth = 76;
    private readonly MainViewModel _viewModel;
    private HwndSource? _source;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        SidebarHost.Width = viewModel.IsSidebarCollapsed ? CollapsedWidth : ExpandedWidth;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _source = PresentationSource.FromVisual(this) as HwndSource;
        _source?.AddHook(WndProc);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _source?.RemoveHook(WndProc);
        _source = null;
        base.OnClosed(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_viewModel.AllowExit && _viewModel.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (MaximizeIcon is not null)
        {
            MaximizeIcon.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsSidebarCollapsed))
        {
            AnimateSidebar(_viewModel.IsSidebarCollapsed);
        }
    }

    private void AnimateSidebar(bool collapsed)
    {
        SidebarHost.BeginAnimation(WidthProperty, new DoubleAnimation
        {
            To = collapsed ? CollapsedWidth : ExpandedWidth,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void MinimizeClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.MinimizeToTray)
        {
            Hide();
            return;
        }

        WindowState = WindowState.Minimized;
    }

    private void MaximizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int wmGetMinMaxInfo = 0x0024;
        if (msg == wmGetMinMaxInfo)
        {
            WorkAreaMaximizer.Apply(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }
}

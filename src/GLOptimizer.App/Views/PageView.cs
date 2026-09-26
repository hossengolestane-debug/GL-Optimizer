using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace GLOptimizer.App.Views;

public class PageView : UserControl
{
    protected PageView()
    {
        Opacity = 0;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BeginAnimation(OpacityProperty, new DoubleAnimation
        {
            From = 0,
            To = 1,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });
    }
}

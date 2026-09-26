using System.Windows;
using System.Windows.Controls;

namespace GLOptimizer.App.Controls;

public partial class ChartCard : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(ChartCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle), typeof(string), typeof(ChartCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty EmptyMessageProperty = DependencyProperty.Register(
        nameof(EmptyMessage), typeof(string), typeof(ChartCard), new PropertyMetadata("No data."));

    public static readonly DependencyProperty HasDataProperty = DependencyProperty.Register(
        nameof(HasData), typeof(bool), typeof(ChartCard), new PropertyMetadata(false));

    public ChartCard() => InitializeComponent();

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string EmptyMessage
    {
        get => (string)GetValue(EmptyMessageProperty);
        set => SetValue(EmptyMessageProperty, value);
    }

    public bool HasData
    {
        get => (bool)GetValue(HasDataProperty);
        set => SetValue(HasDataProperty, value);
    }
}

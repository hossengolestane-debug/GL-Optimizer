using System.Windows;
using System.Windows.Controls;

namespace GLOptimizer.App.Controls;

public partial class SettingsRow : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(SettingsRow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(SettingsRow), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SlotProperty = DependencyProperty.Register(
        nameof(Slot), typeof(object), typeof(SettingsRow), new PropertyMetadata(null));

    public SettingsRow() => InitializeComponent();

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public object? Slot
    {
        get => GetValue(SlotProperty);
        set => SetValue(SlotProperty, value);
    }
}

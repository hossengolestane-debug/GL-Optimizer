using System.Windows;
using System.Windows.Controls;
using GLOptimizer.Core.Diagnostics;

namespace GLOptimizer.App.Controls;

public partial class BackupItem : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(BackupItem), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty WhenProperty = DependencyProperty.Register(
        nameof(When), typeof(string), typeof(BackupItem), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SizeTextProperty = DependencyProperty.Register(
        nameof(SizeText), typeof(string), typeof(BackupItem), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
        nameof(StatusText), typeof(string), typeof(BackupItem), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind), typeof(StatusKind), typeof(BackupItem), new PropertyMetadata(StatusKind.Neutral));

    public BackupItem() => InitializeComponent();

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string When
    {
        get => (string)GetValue(WhenProperty);
        set => SetValue(WhenProperty, value);
    }

    public string SizeText
    {
        get => (string)GetValue(SizeTextProperty);
        set => SetValue(SizeTextProperty, value);
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public StatusKind Kind
    {
        get => (StatusKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }
}

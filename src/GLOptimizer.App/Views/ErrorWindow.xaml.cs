using System.Windows;

namespace GLOptimizer.App.Views;

public partial class ErrorWindow : Window
{
    public ErrorWindow(string summary, string detail)
    {
        InitializeComponent();
        DataContext = new ErrorNotice { Summary = summary, Detail = detail };
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private void CopyClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is ErrorNotice notice && !string.IsNullOrWhiteSpace(notice.Detail))
        {
            Clipboard.SetText(notice.Detail);
        }
    }
}

public sealed class ErrorNotice
{
    public required string Summary { get; init; }

    public required string Detail { get; init; }
}

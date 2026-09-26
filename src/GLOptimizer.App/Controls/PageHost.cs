using System.Windows;
using System.Windows.Controls;
using GLOptimizer.App.ViewModels;

namespace GLOptimizer.App.Controls;

/// <summary>
/// Hosts the current page. A measure or arrange failure is logged and replaced with a plain error
/// message so one page cannot take down the shell. The failure stays visible and is counted.
/// </summary>
public sealed class PageHost : ContentControl
{
    private bool _failed;

    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        if (!_failed || ReferenceEquals(oldContent, newContent))
        {
            return;
        }

        _failed = false;
        if (GetTemplateChild("Presenter") is UIElement presenter)
        {
            presenter.Visibility = Visibility.Visible;
        }

        if (GetTemplateChild("Error") is UIElement error)
        {
            error.Visibility = Visibility.Collapsed;
        }
    }

    protected override Size MeasureOverride(Size constraint)
    {
        if (_failed)
        {
            return base.MeasureOverride(constraint);
        }

        try
        {
            return base.MeasureOverride(constraint);
        }
        catch (Exception ex) when (!PageLayoutFailures.IsFatal(ex))
        {
            CollapseFailedContent(ex);
            return base.MeasureOverride(constraint);
        }
    }

    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        if (_failed)
        {
            return base.ArrangeOverride(arrangeBounds);
        }

        try
        {
            return base.ArrangeOverride(arrangeBounds);
        }
        catch (Exception ex) when (!PageLayoutFailures.IsFatal(ex))
        {
            CollapseFailedContent(ex);
            return base.ArrangeOverride(arrangeBounds);
        }
    }

    private void CollapseFailedContent(Exception exception)
    {
        if (_failed)
        {
            return;
        }

        _failed = true;
        if (GetTemplateChild("Presenter") is not UIElement presenter || GetTemplateChild("Error") is not UIElement error)
        {
            _failed = false;
            throw new InvalidOperationException("The page host template is missing its presenter.", exception);
        }

        presenter.Visibility = Visibility.Collapsed;
        error.Visibility = Visibility.Visible;
        var name = Content is IPageViewModel page ? page.Page.ToString() : "unknown";
        if (GetTemplateChild("ErrorText") is TextBlock text)
        {
            text.Text = "This page could not be displayed (" + name + "). " + exception.Message;
        }

        PageLayoutFailures.Report(name, exception);
    }
}

internal static class PageLayoutFailures
{
    public const string Marker = "Page layout failed";

    public static int Count { get; private set; }

    public static bool IsFatal(Exception exception) =>
        exception is OutOfMemoryException or AccessViolationException or StackOverflowException;

    public static void Report(string page, Exception exception)
    {
        Count++;
        if (Application.Current is global::GLOptimizer.App.App app)
        {
            app.ReportPageLayoutFailure(Marker + ": " + page, exception);
        }
    }
}

using System.Windows;
using System.Windows.Threading;
using GLOptimizer.App.Composition;
using GLOptimizer.App.Views;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace GLOptimizer.App;

public partial class App : Application
{
    private ServiceProvider? _provider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            _provider = AppHost.Build();
            AppHost.Start(_provider);
            var window = _provider.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();
        }
        catch (Exception ex)
        {
            TryLog(ex, "Startup");
            MessageBox.Show(UserFacingError.From(ex), "GL Optimizer", MessageBoxButton.OK, MessageBoxImage.None);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_provider is not null)
            {
                var log = _provider.GetService<ILogStore>();
                log?.Write(LogSeverity.Information, "App", "GL Optimizer is shutting down.");
                log?.Flush();
                _provider.Dispose();
                _provider = null;
            }
        }
        catch (Exception)
        {
            // Shutdown should not raise another dialog.
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        TryLog(e.Exception, "Unhandled");
        try
        {
            var dialog = new ErrorWindow(UserFacingError.From(e.Exception), e.Exception.Message);
            if (MainWindow is { IsVisible: true })
            {
                dialog.Owner = MainWindow;
            }

            dialog.ShowDialog();
            e.Handled = true;
        }
        catch (Exception)
        {
            e.Handled = true;
            Shutdown(1);
        }
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            TryLog(exception, "AppDomain");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        TryLog(e.Exception, "Task");
        e.SetObserved();
    }

    private void TryLog(Exception exception, string source)
    {
        try
        {
            _provider?.GetService<ILogStore>()?.Write(LogSeverity.Error, source, UserFacingError.From(exception), exception);
        }
        catch (Exception)
        {
            // Logging must not throw out of the exception handler.
        }
    }
}

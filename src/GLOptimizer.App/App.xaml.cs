using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using GLOptimizer.App.Composition;
using GLOptimizer.App.Services;
using GLOptimizer.App.Views;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Elevation;
using GLOptimizer.Core.Logging;
using GLOptimizer.GameLoop;
using Microsoft.Extensions.DependencyInjection;

namespace GLOptimizer.App;

public partial class App : Application
{
    private const int AttachParentProcess = -1;
    private ServiceProvider? _provider;
    private Mutex? _instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        HoldInstance();
        var smoke = SmokeTest.IsRequested(e.Args);
        if (smoke)
        {
            SmokeTest.MarkActive();
        }

        try
        {
            _provider = AppHost.Build();
            AppHost.Start(_provider);
            if (smoke)
            {
                Shutdown(RunSmoke());
                return;
            }

            var operation = ElevationPolicy.Read(e.Args);
            if (operation is not null)
            {
                var launch = _provider.GetRequiredService<ILaunchOptimized>();
                var result = operation == "restore-priority"
                    ? launch.RecoverAsync(restore: true).GetAwaiter().GetResult()
                    : launch.ApplyAsync(confirmed: true).GetAwaiter().GetResult();
                var log = _provider.GetRequiredService<ILogStore>();
                log.Write(
                    result.Succeeded ? LogSeverity.Information : LogSeverity.Warning,
                    "Elevation",
                    result.Succeeded ? operation + " completed." : result.Error ?? operation + " did not complete.");
                Shutdown(result.Succeeded ? 0 : 1);
                return;
            }

            var window = _provider.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();
            _provider.GetRequiredService<TrayHost>();
        }
        catch (Exception ex)
        {
            TryLog(ex, "Startup");
            if (smoke)
            {
                Shutdown(1);
                return;
            }

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
                _provider.GetService<IMonitoringCoordinator>()?.Stop();
                _provider.Dispose();
                _provider = null;
            }
        }
        catch (Exception)
        {
            // Shutdown should not raise another dialog.
        }

        try
        {
            _instance?.Dispose();
            _instance = null;
        }
        catch (Exception)
        {
            // Releasing the install mutex must not block exit.
        }

        base.OnExit(e);
    }

    private int RunSmoke()
    {
        var provider = _provider ?? throw new InvalidOperationException("The host was not built.");
        _ = provider.GetRequiredService<MainWindow>();
        var log = provider.GetRequiredService<ILogStore>();
        log.Write(LogSeverity.Information, "Smoke", SmokeTest.Marker);
        log.Flush();
        WriteMarker(SmokeTest.Marker);
        provider.GetService<IMonitoringCoordinator>()?.Stop();
        provider.GetService<LaunchSessionWatcher>()?.Dispose();
        return 0;
    }

    private void HoldInstance()
    {
        try
        {
            _instance = new Mutex(false, "GLOptimizer");
        }
        catch (Exception)
        {
            _instance = null;
        }
    }

    private static void WriteMarker(string line)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            if (!AttachConsole(AttachParentProcess))
            {
                return;
            }

            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            Console.WriteLine(line);
        }
        catch (Exception)
        {
            // The log line is the marker when the parent has no console.
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        TryLog(e.Exception, "Unhandled");
        if (SmokeTest.Active)
        {
            e.Handled = true;
            Shutdown(1);
            return;
        }

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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);
}

using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using GLOptimizer.App.Composition;
using GLOptimizer.App.Controls;
using GLOptimizer.App.Services;
using GLOptimizer.App.ViewModels;
using GLOptimizer.App.Views;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Elevation;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Navigation;
using GLOptimizer.GameLoop;
using Microsoft.Extensions.DependencyInjection;

namespace GLOptimizer.App;

public partial class App : Application
{
    private const int AttachParentProcess = -1;
    private ServiceProvider? _provider;
    private Mutex? _instance;
    private bool _inSmoke;
    private string? _smokeDispatcherFailure;

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
                WriteLines(ex.ToString());
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
        _inSmoke = true;
        try
        {
            return RunSmokeCore();
        }
        finally
        {
            _inSmoke = false;
        }
    }

    private int RunSmokeCore()
    {
        var provider = _provider ?? throw new InvalidOperationException("The host was not built.");
        var listener = new BindingFailureListener();
        Watch(PresentationTraceSources.DataBindingSource, listener);
        Watch(PresentationTraceSources.ResourceDictionarySource, listener);
        var log = provider.GetRequiredService<ILogStore>();
        var window = provider.GetRequiredService<MainWindow>();
        MainWindow = window;
        if (window.DataContext is not MainViewModel viewModel)
        {
            return FinishSmoke(provider, log, ["The main window has no view model."]);
        }

        viewModel.AllowExit = true;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.ShowInTaskbar = false;
        window.Left = -20000;
        window.Top = -20000;
        window.Show();
        Layout(window);
        Pump(DispatcherPriority.ContextIdle);

        var failures = new List<string>();
        CollectBindings(listener, failures, "Startup");
        if (viewModel.ShowFirstRun)
        {
            viewModel.DismissFirstRunCommand.Execute(null);
            Layout(window);
            Pump(DispatcherPriority.ContextIdle);
        }

        var pages = Enum.GetValues<AppPage>();
        LayoutPages(window, viewModel, listener, failures, pages);
        var found = WaitForGameLoopAbsence(log);
        LayoutPages(window, viewModel, listener, failures, pages);
        CollectBindings(listener, failures, "Final");
        if (PageLayoutFailures.Count > 0)
        {
            failures.Add(PageLayoutFailures.Marker + " " + PageLayoutFailures.Count.ToString(CultureInfo.InvariantCulture) + " time(s).");
        }

        if (_smokeDispatcherFailure is not null)
        {
            failures.Add(_smokeDispatcherFailure);
        }

        if (!found)
        {
            viewModel.NavigateCommand.Execute(AppPage.Dashboard);
            var status = viewModel.CurrentViewModel is DashboardViewModel dashboard ? dashboard.StatusLine : "dashboard was not shown";
            failures.Add("The not-installed GameLoop path did not render. Status: " + status);
        }

        return FinishSmoke(provider, log, failures);
    }

    internal void ReportPageLayoutFailure(string message, Exception exception) =>
        TryLog(exception, "Page", message);

    private static void LayoutPages(
        Window window,
        MainViewModel viewModel,
        BindingFailureListener listener,
        List<string> failures,
        Array pages)
    {
        foreach (AppPage page in pages)
        {
            try
            {
                viewModel.NavigateCommand.Execute(page);
                Layout(window);
                Pump(DispatcherPriority.ContextIdle);
            }
            catch (Exception ex) when (!PageLayoutFailures.IsFatal(ex))
            {
                failures.Add(page + " layout: " + ex);
            }

            CollectBindings(listener, failures, page.ToString());
        }
    }

    private static bool WaitForGameLoopAbsence(ILogStore log)
    {
        var deadline = DateTime.UtcNow.AddSeconds(40);
        while (DateTime.UtcNow < deadline)
        {
            Pump(DispatcherPriority.ContextIdle);
            if (GameLoopWasNotFound(log))
            {
                return true;
            }

            PumpFor(TimeSpan.FromMilliseconds(250));
        }

        return GameLoopWasNotFound(log);
    }

    private static bool GameLoopWasNotFound(ILogStore log)
    {
        log.Flush();
        foreach (var entry in log.ReadActiveLog(2000))
        {
            if (entry.Message.Contains("GameLoop was not found.", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private int FinishSmoke(ServiceProvider provider, ILogStore log, List<string> failures)
    {
        try
        {
            if (failures.Count == 0)
            {
                log.Write(LogSeverity.Information, "Smoke", SmokeTest.Marker);
                log.Flush();
                WriteLines(SmokeTest.Marker);
                return 0;
            }

            foreach (var failure in failures)
            {
                log.Write(LogSeverity.Error, "Smoke", failure);
            }

            log.Flush();
            var lines = new List<string> { "GL Optimizer smoke test failed" };
            lines.AddRange(failures);
            WriteLines(lines.ToArray());
            return 1;
        }
        finally
        {
            provider.GetService<IMonitoringCoordinator>()?.Stop();
            provider.GetService<LaunchSessionWatcher>()?.Dispose();
        }
    }

    private static void Layout(Window window)
    {
        var width = window.Width > 0 ? window.Width : window.MinWidth;
        var height = window.Height > 0 ? window.Height : window.MinHeight;
        var size = new Size(width, height);
        window.Measure(size);
        window.Arrange(new Rect(size));
        window.UpdateLayout();
        Pump(DispatcherPriority.Render);
    }

    private static void Pump(DispatcherPriority priority)
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(priority, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }

    private static void PumpFor(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = duration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void Watch(TraceSource source, BindingFailureListener listener)
    {
        source.Switch.Level = SourceLevels.Error;
        source.Listeners.Add(listener);
    }

    private static void CollectBindings(BindingFailureListener listener, List<string> failures, string scope)
    {
        while (listener.Reported < listener.Errors.Count)
        {
            failures.Add(scope + " binding: " + listener.Errors[listener.Reported]);
            listener.Reported++;
        }
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

    private static void WriteLines(params string[] lines)
    {
        if (!OperatingSystem.IsWindows() || lines.Length == 0)
        {
            return;
        }

        try
        {
            if (!AttachConsole(AttachParentProcess) && Marshal.GetLastWin32Error() != 5)
            {
                return;
            }

            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
            foreach (var line in lines)
            {
                Console.WriteLine(line);
            }
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
            _smokeDispatcherFailure ??= e.Exception.ToString();
            if (!_inSmoke)
            {
                WriteLines(_smokeDispatcherFailure);
                Shutdown(1);
            }

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

    private void TryLog(Exception exception, string source, string? message = null)
    {
        try
        {
            _provider?.GetService<ILogStore>()?.Write(LogSeverity.Error, source, message ?? UserFacingError.From(exception), exception);
        }
        catch (Exception)
        {
            // Logging must not throw out of the exception handler.
        }
    }

    private sealed class BindingFailureListener : TraceListener
    {
        public List<string> Errors { get; } = new();

        public int Reported { get; set; }

        public override void Write(string? message) => Add(message);

        public override void WriteLine(string? message) => Add(message);

        public override void Fail(string? message) => Add(message);

        public override void Fail(string? message, string? detailMessage) => Add(message + " " + detailMessage);

        public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
        {
            if (eventType is TraceEventType.Error or TraceEventType.Critical)
            {
                Add(source + " " + message);
            }
        }

        public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args)
        {
            if (eventType is not (TraceEventType.Error or TraceEventType.Critical))
            {
                return;
            }

            var text = format;
            if (format is not null && args is { Length: > 0 })
            {
                try
                {
                    text = string.Format(CultureInfo.InvariantCulture, format, args);
                }
                catch (FormatException)
                {
                    text = format;
                }
            }

            Add(source + " " + text);
        }

        private void Add(string? message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Errors.Add(message);
            }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);
}

using System.Runtime.Versioning;
using GLOptimizer.App.ViewModels;
using GLOptimizer.App.Views;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Elevation;
using GLOptimizer.Core.Notifications;

namespace GLOptimizer.App.Services;

public sealed class TrayHost : IDisposable
{
    private readonly MainWindow _window;
    private readonly MainViewModel _shell;
    private readonly ISettingsStore _settings;
    private readonly IMonitoringCoordinator _monitoring;
    private readonly IGameLoopDetector _detector;
    private readonly IGameLoopLauncher _launcher;
    private readonly ILaunchOptimized _launch;
    private readonly IElevationRelaunch _elevation;
    private readonly IUserConfirmation _confirm;
    private readonly IToastCenter _toasts;
    private readonly object? _icon;

    public TrayHost(
        MainWindow window,
        MainViewModel shell,
        ISettingsStore settings,
        IMonitoringCoordinator monitoring,
        IGameLoopDetector detector,
        IGameLoopLauncher launcher,
        ILaunchOptimized launch,
        IElevationRelaunch elevation,
        IUserConfirmation confirm,
        IToastCenter toasts)
    {
        _window = window;
        _shell = shell;
        _settings = settings;
        _monitoring = monitoring;
        _detector = detector;
        _launcher = launcher;
        _launch = launch;
        _elevation = elevation;
        _confirm = confirm;
        _toasts = toasts;
        if (OperatingSystem.IsWindows())
        {
            _icon = CreateIcon();
        }
    }

    public void Dispose()
    {
        if (OperatingSystem.IsWindows())
        {
            DisposeIcon();
        }
    }

    [SupportedOSPlatform("windows")]
    private System.Windows.Forms.NotifyIcon CreateIcon()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open GL Optimizer", null, (_, _) => ShowWindow());
        menu.Items.Add("Launch GameLoop", null, (_, _) => _ = LaunchGameLoopAsync());
        menu.Items.Add("Launch Optimized", null, (_, _) => _ = LaunchOptimizedAsync());
        var monitoring = new System.Windows.Forms.ToolStripMenuItem("Monitoring")
        {
            CheckOnClick = true,
            Checked = _settings.Current.MonitoringEnabled
        };
        monitoring.CheckedChanged += (_, _) => ToggleMonitoring(monitoring.Checked);
        menu.Items.Add(monitoring);
        menu.Items.Add("Exit", null, (_, _) => Exit());
        var icon = new System.Windows.Forms.NotifyIcon
        {
            Text = "GL Optimizer",
            Visible = true,
            ContextMenuStrip = menu,
            Icon = LoadIcon()
        };
        icon.DoubleClick += (_, _) => ShowWindow();
        return icon;
    }

    [SupportedOSPlatform("windows")]
    private static System.Drawing.Icon LoadIcon()
    {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(path))
        {
            var extracted = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (extracted is not null)
            {
                return extracted;
            }
        }

        return System.Drawing.SystemIcons.Application;
    }

    [SupportedOSPlatform("windows")]
    private void DisposeIcon()
    {
        if (_icon is System.Windows.Forms.NotifyIcon icon)
        {
            icon.Visible = false;
            icon.Dispose();
        }
    }

    private void ShowWindow()
    {
        _window.Show();
        if (_window.WindowState == System.Windows.WindowState.Minimized)
        {
            _window.WindowState = System.Windows.WindowState.Normal;
        }

        _window.Activate();
    }

    private void Exit()
    {
        _shell.AllowExit = true;
        _window.Close();
    }

    private void ToggleMonitoring(bool enabled)
    {
        var settings = _settings.Current;
        settings.MonitoringEnabled = enabled;
        var saved = _settings.Save(settings);
        if (!saved.Succeeded)
        {
            return;
        }

        _monitoring.Refresh();
    }

    private async Task LaunchGameLoopAsync()
    {
        var detected = await _detector.DetectAsync();
        if (!detected.Succeeded || detected.Value is null)
        {
            return;
        }

        var installs = detected.Value.Installations
            .Where(installation => !string.IsNullOrWhiteSpace(installation.LauncherPath))
            .ToArray();
        if (installs.Length != 1)
        {
            return;
        }

        var started = await _launcher.StartAsync(installs[0]);
        if (started.Succeeded)
        {
            _toasts.Show(ToastCatalog.GameLoopStarted);
        }
    }

    private async Task LaunchOptimizedAsync()
    {
        if (!_confirm.Confirm(
                "Launch Optimized",
                "Set verified GameLoop processes to AboveNormal. Realtime is never set. Priorities return when those processes exit, or you can restore them from the recovery banner."))
        {
            return;
        }

        var result = await _launch.ApplyAsync(confirmed: true);
        if (!result.Succeeded && result.Error?.Contains("elevation", StringComparison.OrdinalIgnoreCase) == true
            && _confirm.Confirm("Elevation", result.Error))
        {
            _elevation.Relaunch("launch-optimized");
        }
    }
}

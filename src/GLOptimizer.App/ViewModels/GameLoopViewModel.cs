using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.App.Services;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Configuration;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Elevation;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Navigation;
using GLOptimizer.Core.Notifications;
using GLOptimizer.Core.Repair;
using GLOptimizer.GameLoop;

namespace GLOptimizer.App.ViewModels;

public partial class GameLoopViewModel : PageViewModel, IRefreshable
{
    private readonly IGameLoopDetector _detector;
    private readonly IGameLoopLauncher _launcher;
    private readonly IGameLoopConfigDiscovery _config;
    private readonly IUserConfirmation _confirm;
    private readonly ILaunchOptimized _launch;
    private readonly IElevationRelaunch _elevation;
    private readonly IToastCenter _toasts;
    private readonly ILogStore _log;
    private readonly ScanSession _session = new();
    private GameLoopConfigReport? _report;

    public GameLoopViewModel(
        IGameLoopDetector detector,
        IGameLoopLauncher launcher,
        IGameLoopConfigDiscovery config,
        IUserConfirmation confirm,
        ILaunchOptimized launch,
        IElevationRelaunch elevation,
        IToastCenter toasts,
        ILogStore log)
        : base(AppPage.GameLoop)
    {
        _detector = detector;
        _launcher = launcher;
        _config = config;
        _confirm = confirm;
        _launch = launch;
        _elevation = elevation;
        _toasts = toasts;
        _log = log;
        ConfigRows.FillSettings(ConfigSettings, null);
    }

    public string RuntimeNote =>
        ElevationPolicy.RunsAsInvoker
        + " "
        + (_launch.PowerPlan().Error ?? "Power plan changes are not implemented.")
        + " "
        + (_launch.GraphicsPreference().Error ?? "Graphics preference changes are not implemented.");

    public string SafetyNote => Phase0Notices.Safety + " " + Phase0Notices.ReadOnlyGameLoop;

    public ObservableCollection<InstallItem> Installs { get; } = new();

    public ObservableCollection<ProcessRow> Processes { get; } = new();

    public ObservableCollection<DiagnosticRowModel> ConfigSettings { get; } = new();

    public ObservableCollection<DiagnosticRowModel> ConfigFiles { get; } = new();

    public ObservableCollection<DiagnosticRowModel> SharedSettings { get; } = new();

    public ObservableCollection<DiagnosticRowModel> SharedFiles { get; } = new();

    public bool HasInstalls => Installs.Count > 0;

    public bool HasConfigFiles => ConfigFiles.Count > 0;

    public bool HasSharedConfig => SharedFiles.Count > 0;

    [ObservableProperty]
    private string _statusLine = "Scanning…";

    [ObservableProperty]
    private string _actionMessage = "Close and restart stop only processes inside the verified install. Force stop asks again.";

    [ObservableProperty]
    private string _configNotice = "Scanning…";

    [ObservableProperty]
    private string _sharedNotice = string.Empty;

    [ObservableProperty]
    private InstallItem? _selectedInstall;

    [ObservableProperty]
    private bool _isScanning;

    partial void OnSelectedInstallChanged(InstallItem? value)
    {
        Processes.Clear();
        if (value is not null)
        {
            foreach (var process in value.Processes)
            {
                Processes.Add(process);
            }
        }

        StartCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasProcesses));
        RenderConfig();
    }

    public bool HasProcesses => Processes.Count > 0;

    [RelayCommand]
    private Task ScanAsync() => RunScanAsync();

    [RelayCommand]
    private void CancelScan() => _session.Cancel();

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (SelectedInstall?.Installation is null)
        {
            return;
        }

        var result = await _launcher.StartAsync(SelectedInstall.Installation);
        ActionMessage = result.Succeeded
            ? "Start requested for the verified launcher."
            : result.Error ?? "GameLoop could not be started.";
        if (result.Succeeded)
        {
            _toasts.Show(ToastCatalog.GameLoopStarted);
        }
    }

    [RelayCommand]
    private async Task LaunchOptimizedAsync()
    {
        if (!_confirm.Confirm(
                "Launch Optimized",
                "Set verified GameLoop processes to AboveNormal. Realtime is never set. The change lasts while those processes run, then the journal is cleared. A crash leaves the journal so the next startup can restore the saved priorities."))
        {
            ActionMessage = "Launch Optimized was not confirmed.";
            return;
        }

        var result = await _launch.ApplyAsync(confirmed: true);
        if (result.Succeeded)
        {
            ActionMessage = "AboveNormal was applied to verified GameLoop processes.";
            return;
        }

        ActionMessage = result.Error ?? "Launch Optimized could not be applied.";
        if (result.Error?.Contains("elevation", StringComparison.OrdinalIgnoreCase) == true
            && _confirm.Confirm("Elevation", ActionMessage + " Relaunch only this operation with elevation?"))
        {
            var elevated = _elevation.Relaunch("launch-optimized");
            ActionMessage = elevated.Succeeded
                ? "Elevation was requested for Launch Optimized only."
                : elevated.Error ?? ActionMessage;
        }
    }

    private bool CanStart() => !string.IsNullOrWhiteSpace(SelectedInstall?.Installation.LauncherPath);

    [RelayCommand]
    private async Task RestartAsync()
    {
        if (SelectedInstall?.Installation is null)
        {
            ActionMessage = "Select a verified GameLoop install first.";
            return;
        }

        if (!_confirm.Confirm(
                "Restart GameLoop",
                "Stop verified GameLoop processes and then start the launcher. A COD Mobile or PUBG Mobile session can close with them. Unrelated processes are not touched."))
        {
            ActionMessage = "Restart was not confirmed.";
            return;
        }

        var result = await _launcher.RestartAsync(SelectedInstall.Installation, forceConfirmed: false);
        if (!result.Succeeded && result.Error == ProcessStopMessages.ForceRequired)
        {
            if (!_confirm.Confirm("Force stop", "GameLoop did not close. Force stop only processes whose executable path is inside the verified install?"))
            {
                ActionMessage = "Force stop was not confirmed. GameLoop was not restarted.";
                return;
            }

            result = await _launcher.RestartAsync(SelectedInstall.Installation, forceConfirmed: true);
        }

        ActionMessage = result.Succeeded
            ? "GameLoop was stopped and the verified launcher was started."
            : result.Error ?? "GameLoop could not be restarted.";
    }

    [RelayCommand]
    private async Task CloseAsync()
    {
        if (SelectedInstall?.Installation is null)
        {
            ActionMessage = "Select a verified GameLoop install first.";
            return;
        }

        if (!_confirm.Confirm(
                "Close GameLoop",
                "Stop GameLoop processes whose executable path is inside the verified install. A COD Mobile or PUBG Mobile session can close with them. Unrelated processes are not touched."))
        {
            ActionMessage = "Close was not confirmed.";
            return;
        }

        var result = await _launcher.CloseAsync(SelectedInstall.Installation, forceConfirmed: false);
        if (!result.Succeeded && result.Error == ProcessStopMessages.ForceRequired)
        {
            if (!_confirm.Confirm("Force stop", "GameLoop did not close. Force stop only processes whose executable path is inside the verified install?"))
            {
                ActionMessage = "Force stop was not confirmed.";
                return;
            }

            result = await _launcher.CloseAsync(SelectedInstall.Installation, forceConfirmed: true);
        }

        ActionMessage = result.Succeeded
            ? "Verified GameLoop processes were asked to close."
            : result.Error ?? "GameLoop could not be closed.";
    }

    public void Refresh() => _ = RunScanAsync();

    private async Task RunScanAsync()
    {
        var (generation, token) = _session.Start();
        try
        {
            IsScanning = true;
            StatusLine = "Scanning…";
            var result = await _detector.DetectAsync(token);
            if (!_session.IsCurrent(generation))
            {
                return;
            }

            Installs.Clear();
            _report = null;
            if (!result.Succeeded || result.Value is null)
            {
                StatusLine = result.Error ?? "GameLoop could not be scanned.";
                ConfigNotice = "Configuration was not searched.";
                SelectedInstall = null;
            }
            else
            {
                foreach (var installation in result.Value.Installations)
                {
                    Installs.Add(InstallItem.From(installation));
                }

                StatusLine = result.Value.Installations.Count == 0
                    ? "GameLoop was not found."
                    : result.Value.Installations.Count.ToString(CultureInfo.InvariantCulture) + " GameLoop installation(s) found.";
                ScanCheckLog.Write(_log, result.Value);
                if (result.Value.BrokenRegistration)
                {
                    StatusLine += " An uninstall entry points at a missing path.";
                }

                var paths = GameLoopLocations.InstallAndData(result.Value);
                var config = await _config.DiscoverAsync(paths, token);
                if (!_session.IsCurrent(generation))
                {
                    return;
                }

                if (!config.Succeeded || config.Value is null)
                {
                    ConfigNotice = config.Error ?? "Configuration could not be read.";
                }
                else
                {
                    _report = config.Value;
                    ConfigNotice = config.Value.Notice ?? "Configuration was read. Files were not modified.";
                }

                SelectedInstall = Installs.Count > 0 ? Installs[0] : null;
            }

            OnPropertyChanged(nameof(HasInstalls));
            RenderConfig();
        }
        catch (Exception ex)
        {
            if (_session.IsCurrent(generation))
            {
                StatusLine = "The scan could not finish. " + ex.Message;
                ConfigNotice = "Configuration was not searched.";
            }
        }
        finally
        {
            if (_session.IsCurrent(generation))
            {
                IsScanning = false;
            }
        }
    }

    private void RenderConfig()
    {
        InstallConfigReport? match = null;
        var path = SelectedInstall?.Installation.InstallPath;
        if (_report is not null && !string.IsNullOrWhiteSpace(path))
        {
            foreach (var install in _report.Installs)
            {
                if (string.Equals(install.InstallPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    match = install;
                    break;
                }
            }
        }

        ConfigRows.FillSettings(ConfigSettings, match?.Settings);
        ConfigFiles.Clear();
        if (match is not null)
        {
            foreach (var file in match.Files)
            {
                ConfigFiles.Add(ConfigRows.File(file));
            }
        }

        SharedSettings.Clear();
        SharedFiles.Clear();
        SharedNotice = string.Empty;
        if (_report is not null && _report.SharedFiles.Count > 0)
        {
            SharedNotice = _report.Notice ?? "User configuration is listed separately.";
            ConfigRows.FillSettings(SharedSettings, _report.SharedSettings);
            foreach (var file in _report.SharedFiles)
            {
                SharedFiles.Add(ConfigRows.File(file));
            }
        }

        OnPropertyChanged(nameof(HasConfigFiles));
        OnPropertyChanged(nameof(HasSharedConfig));
    }
}

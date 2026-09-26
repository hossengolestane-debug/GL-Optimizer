using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Navigation;

namespace GLOptimizer.App.ViewModels;

public partial class GameLoopViewModel : PageViewModel, IRefreshable
{
    private readonly IGameLoopDetector _detector;
    private readonly IGameLoopLauncher _launcher;
    private readonly ScanSession _session = new();

    public GameLoopViewModel(IGameLoopDetector detector, IGameLoopLauncher launcher)
        : base(AppPage.GameLoop)
    {
        _detector = detector;
        _launcher = launcher;
    }

    public string SafetyNote => Phase0Notices.Safety + " " + Phase0Notices.ReadOnlyGameLoop;

    public ObservableCollection<InstallItem> Installs { get; } = new();

    public ObservableCollection<ProcessRow> Processes { get; } = new();

    public bool HasInstalls => Installs.Count > 0;

    [ObservableProperty]
    private string _statusLine = "Scanning…";

    [ObservableProperty]
    private string _actionMessage = "Close and restart are not implemented. They do not stop a process.";

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

        var result = await _launcher.RestartAsync(SelectedInstall.Installation);
        ActionMessage = result.Error ?? "Restart GameLoop is not implemented.";
    }

    [RelayCommand]
    private async Task CloseAsync()
    {
        if (SelectedInstall?.Installation is null)
        {
            ActionMessage = "Select a verified GameLoop install first.";
            return;
        }

        var result = await _launcher.CloseAsync(SelectedInstall.Installation);
        ActionMessage = result.Error ?? "Close GameLoop is not implemented.";
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
            if (!result.Succeeded || result.Value is null)
            {
                StatusLine = result.Error ?? "GameLoop could not be scanned.";
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
                if (result.Value.BrokenRegistration)
                {
                    StatusLine += " An uninstall entry points at a missing path.";
                }

                SelectedInstall = Installs.Count > 0 ? Installs[0] : null;
            }

            OnPropertyChanged(nameof(HasInstalls));
        }
        catch (Exception ex)
        {
            if (_session.IsCurrent(generation))
            {
                StatusLine = "The scan could not finish. " + ex.Message;
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
}

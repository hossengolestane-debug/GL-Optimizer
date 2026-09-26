using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.App.Services;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Navigation;
using GLOptimizer.Core.Repair;
using GLOptimizer.Core.Results;
using GLOptimizer.GameLoop;

namespace GLOptimizer.App.ViewModels;

public partial class AppMarketViewModel : PageViewModel, IRefreshable
{
    private readonly IAppMarketDiagnostics _diagnostics;
    private readonly IAppMarketRepair _repair;
    private readonly IUserConfirmation _confirm;
    private readonly IGameLoopLauncher _launcher;
    private readonly IGameLoopDetector _detector;
    private readonly ScanSession _session = new();
    private CancellationTokenSource? _repairCancellation;

    public AppMarketViewModel(
        IAppMarketDiagnostics diagnostics,
        IAppMarketRepair repair,
        IUserConfirmation confirm,
        IGameLoopLauncher launcher,
        IGameLoopDetector detector)
        : base(AppPage.AppMarket)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(repair);
        ArgumentNullException.ThrowIfNull(confirm);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(detector);
        _diagnostics = diagnostics;
        _repair = repair;
        _confirm = confirm;
        _launcher = launcher;
        _detector = detector;
    }

    public string SafetyNote => Phase0Notices.Safety + " " + Phase0Notices.NoAppMarketIo;

    public string RepairNote =>
        "Dry run lists the stop, backup, clear, and restart steps. Repair moves only re-validated cache into a backup quarantine. No game data will be removed. GameLoop is not started automatically.";

    public ObservableCollection<MarketItemRow> Inventory { get; } = new();

    public ObservableCollection<MarketItemRow> RepairTargets { get; } = new();

    public ObservableCollection<string> PlannedChanges { get; } = new();

    [ObservableProperty]
    private string _statusText = "UNKNOWN";

    [ObservableProperty]
    private string _installedVersion = "Unknown";

    [ObservableProperty]
    private string _marketVersion = "Unknown";

    [ObservableProperty]
    private string _officialVersion = "Unknown";

    [ObservableProperty]
    private string _officialDetail = "Not requested. Check Version on the COD Mobile page is the only control that contacts a host.";

    [ObservableProperty]
    private string _lastScan = "Unknown";

    [ObservableProperty]
    private string _issue = "Scanning…";

    [ObservableProperty]
    private string _dryRunText = "Run DRY RUN REPAIR to list what would stop, what would be backed up, what would be cleared, and that GameLoop is not started automatically.";

    [ObservableProperty]
    private string _repairMessage = "Repair asks for confirmation, then stops only verified GameLoop processes.";

    [ObservableProperty]
    private string _toast = string.Empty;

    [ObservableProperty]
    private bool _showToast;

    [ObservableProperty]
    private bool _isRepairing;

    [ObservableProperty]
    private string _progressMessage = string.Empty;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private bool _offerStart;

    [ObservableProperty]
    private bool _awaitingRefresh;

    partial void OnIsRepairingChanged(bool value) => NotifyCommands();

    partial void OnOfferStartChanged(bool value) => StartGameLoopCommand.NotifyCanExecuteChanged();

    public void Refresh() => _ = ScanAsync();

    [RelayCommand(CanExecute = nameof(Idle))]
    private Task CheckAgainAsync() => ScanAsync();

    [RelayCommand(CanExecute = nameof(Idle))]
    private async Task DryRunAsync()
    {
        ShowToast = false;
        Toast = string.Empty;
        RepairMessage = "Checking the verified cache…";
        var result = await _repair.DryRunAsync();
        if (!result.Succeeded || result.Value is null)
        {
            DryRunText = result.Error ?? "The dry run could not be completed.";
            RepairMessage = DryRunText;
            PlannedChanges.Clear();
            return;
        }

        ShowPlan(result.Value);
        RepairMessage = result.Value.NeedsReview
            ? result.Value.ReviewReason ?? "The dry run needs review. Nothing was changed."
            : result.Value.CanRepair
                ? "Dry run only. No files were moved."
                : "No verified cache was selected, so nothing would be changed.";
    }

    [RelayCommand(CanExecute = nameof(Idle))]
    private async Task RepairAsync()
    {
        if (!_confirm.Confirm(
                "Repair App Market",
                "Stop verified GameLoop processes, copy metadata, and move verified cache into a backup quarantine. No game data will be removed. GameLoop is not started automatically. A COD Mobile or PUBG Mobile session can close if GameLoop is stopped."))
        {
            RepairMessage = "Repair was not confirmed.";
            return;
        }

        ShowToast = false;
        Toast = string.Empty;
        OfferStart = false;
        using var cancellation = new CancellationTokenSource();
        _repairCancellation = cancellation;
        IsRepairing = true;
        Progress = 30;
        ProgressMessage = "Repair is running. Cancel is available until the first file moves. After that the repair finishes or rolls back.";
        try
        {
            var result = await _repair.RepairAsync(confirmed: true, forceConfirmed: false, cancellation.Token);
            if (result.Succeeded && result.Value is { NeedsForceConfirmation: true })
            {
                if (!_confirm.Confirm(
                        "Force stop",
                        "GameLoop did not close. Force stop only processes whose executable path is inside the verified install?"))
                {
                    RepairMessage = "Force stop was not confirmed. Nothing was moved.";
                    return;
                }

                result = await _repair.RepairAsync(confirmed: true, forceConfirmed: true, cancellation.Token);
            }

            ApplyRepairResult(result);
        }
        catch (Exception ex)
        {
            RepairMessage = "The repair could not finish. " + ex.Message;
            ShowToast = false;
            Toast = string.Empty;
        }
        finally
        {
            IsRepairing = false;
            _repairCancellation = null;
            Progress = 0;
            await ScanAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelRepair()
    {
        _repairCancellation?.Cancel();
        ProgressMessage = "Cancellation requested. If a file already moved, the repair finishes or rolls back.";
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartGameLoopAsync()
    {
        if (!_confirm.Confirm(
                "Start GameLoop",
                "Start the verified GameLoop launcher. The repair does not start it automatically."))
        {
            RepairMessage = "Start was not confirmed.";
            return;
        }

        var detected = await _detector.DetectAsync();
        if (!detected.Succeeded || detected.Value is null)
        {
            RepairMessage = detected.Error ?? "GameLoop could not be found.";
            return;
        }

        var installs = detected.Value.Installations
            .Where(installation => !string.IsNullOrWhiteSpace(installation.LauncherPath))
            .ToArray();
        if (installs.Length != 1)
        {
            RepairMessage = installs.Length == 0
                ? "No verified launcher was found."
                : "More than one GameLoop install was found. Start it from the GameLoop page.";
            return;
        }

        var started = await _launcher.StartAsync(installs[0]);
        RepairMessage = started.Succeeded
            ? "Start requested for the verified launcher."
            : started.Error ?? "GameLoop could not be started.";
    }

    [RelayCommand(CanExecute = nameof(Idle))]
    private async Task RecheckAsync()
    {
        var result = await _repair.RecheckAsync();
        await ScanAsync();
        if (!result.Succeeded || result.Value is null)
        {
            RepairMessage = result.Error ?? "The re-check could not be completed.";
            return;
        }

        RepairMessage = result.Value.Message;
        AwaitingRefresh = result.Value.AwaitingRefresh;
        ShowToast = false;
        Toast = string.Empty;
        if (result.Value.Verdict == RepairVerdictKind.RemoteCatalogIssue)
        {
            StatusText = CatalogComparisonLogic.Badge(CatalogComparison.RemoteCatalogIssue);
            Issue = CatalogComparisonLogic.RemoteMessage;
        }
        else if (result.Value.Verdict == RepairVerdictKind.LocalRefreshed)
        {
            StatusText = CatalogComparisonLogic.Badge(CatalogComparison.Match);
        }
    }

    private bool Idle() => !IsRepairing;

    private bool CanCancel() => IsRepairing;

    private bool CanStart() => !IsRepairing && OfferStart;

    private void NotifyCommands()
    {
        CheckAgainCommand.NotifyCanExecuteChanged();
        DryRunCommand.NotifyCanExecuteChanged();
        RepairCommand.NotifyCanExecuteChanged();
        CancelRepairCommand.NotifyCanExecuteChanged();
        StartGameLoopCommand.NotifyCanExecuteChanged();
        RecheckCommand.NotifyCanExecuteChanged();
    }

    private void ApplyRepairResult(OperationResult<AppMarketRepairResult> result)
    {
        if (!result.Succeeded || result.Value is null)
        {
            RepairMessage = result.Error ?? RepairVerdictLogic.FailedMessage;
            ShowToast = false;
            Toast = string.Empty;
            OfferStart = false;
            AwaitingRefresh = false;
            return;
        }

        RepairMessage = result.Value.Message;
        AwaitingRefresh = result.Value.AwaitingRefresh;
        OfferStart = result.Value.Completed;
        var toast = result.Value.Completed && result.Value.Toast == AppMarketRepairService.CompletedToast;
        ShowToast = toast;
        Toast = toast ? result.Value.Toast! : string.Empty;
    }

    private void ShowPlan(AppMarketRepairPlan plan)
    {
        DryRunText = plan.Text;
        PlannedChanges.Clear();
        foreach (var path in plan.StopPaths)
        {
            PlannedChanges.Add("Stop  " + path);
        }

        foreach (var path in plan.BackupPaths)
        {
            PlannedChanges.Add("Back up  " + path);
        }

        foreach (var path in plan.ClearPaths)
        {
            PlannedChanges.Add("Clear  " + path);
        }

        if (plan.NeedsReview)
        {
            PlannedChanges.Add("Needs review  " + (plan.ReviewReason ?? "The repair was not started."));
        }
    }

    private async Task ScanAsync()
    {
        var (generation, token) = _session.Start();
        try
        {
            Issue = "Scanning…";
            var result = await _diagnostics.ScanAsync(checkOfficialVersion: false, token);
            if (!_session.IsCurrent(generation))
            {
                return;
            }

            Inventory.Clear();
            RepairTargets.Clear();
            if (!result.Succeeded || result.Value is null)
            {
                StatusText = "UNKNOWN";
                InstalledVersion = "Unknown";
                MarketVersion = "Unknown";
                OfficialVersion = "Unknown";
                OfficialDetail = "Not requested.";
                LastScan = "Unknown";
                Issue = result.Error ?? "App Market could not be scanned.";
                return;
            }

            var report = result.Value;
            StatusText = report.StatusText;
            InstalledVersion = Text(report.InstalledVersion);
            MarketVersion = Text(report.MarketVersion);
            OfficialVersion = Text(report.OfficialVersion);
            OfficialDetail = report.OfficialDetail ?? "Not requested.";
            LastScan = report.LastScanUtc?.ToString("u", CultureInfo.InvariantCulture) ?? "Unknown";
            Issue = report.Issue ?? report.Detail ?? "No issue text was recorded.";
            if (report.ScanTruncated)
            {
                Issue += " The inventory stopped at the scan limit.";
            }

            foreach (var item in report.Inventory)
            {
                Inventory.Add(MarketItemRow.From(item));
            }

            foreach (var item in report.RepairTargets)
            {
                RepairTargets.Add(MarketItemRow.From(item));
            }
        }
        catch (Exception ex)
        {
            if (_session.IsCurrent(generation))
            {
                StatusText = "UNKNOWN";
                Issue = "The scan could not finish. " + ex.Message;
            }
        }
    }

    private static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
}

public sealed record MarketItemRow(string Path, string Kind, string Confidence, string Size, string Written, string Reason)
{
    public static MarketItemRow From(Core.Models.MarketInventoryItem item)
    {
        var written = item.LastWriteTimeUtc?.ToString("u", CultureInfo.InvariantCulture) ?? "Unknown";
        var size = item.SizeBytes.ToString(CultureInfo.InvariantCulture) + " bytes, "
            + item.FileCount.ToString(CultureInfo.InvariantCulture) + " file(s)";
        return new MarketItemRow(item.RelativePath, item.Kind.ToString(), item.Confidence.ToString(), size, written, item.Reason);
    }
}

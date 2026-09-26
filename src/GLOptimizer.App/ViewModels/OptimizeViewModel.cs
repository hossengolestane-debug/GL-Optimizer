using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.App.Services;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Navigation;
using GLOptimizer.Core.Notifications;
using GLOptimizer.Core.Optimization;

namespace GLOptimizer.App.ViewModels;

public partial class OptimizeViewModel : PageViewModel, IRefreshable
{
    private readonly IOptimizationService _optimization;
    private readonly IBenchmarkService _benchmark;
    private readonly ILogStore _log;
    private readonly IUserConfirmation _confirm;
    private readonly IToastCenter _toasts;
    private readonly OptimizationProfileSelection _selection;
    private bool _suppress;

    public OptimizeViewModel(
        IOptimizationService optimization,
        IBenchmarkService benchmark,
        ILogStore log,
        IUserConfirmation confirm,
        IToastCenter toasts,
        OptimizationProfileSelection selection)
        : base(AppPage.Optimize)
    {
        _optimization = optimization;
        _benchmark = benchmark;
        _log = log;
        _confirm = confirm;
        _toasts = toasts;
        _selection = selection;
        _selectedProfile = selection.Current.ToString();
        BenchmarkMessage = _benchmark.Describe().Error ?? "Benchmark mode is not implemented.";
    }

    public IReadOnlyList<string> Profiles { get; } = ["Performance", "Balanced", "Quality", "Custom"];

    public ObservableCollection<RecommendationRow> Recommendations { get; } = new();

    public ObservableCollection<DiagnosticRowModel> PreviewRows { get; } = new();

    public string SafetyNote => Phase0Notices.NoOptimization;

    public bool HasPreview => PreviewRows.Count > 0;

    [ObservableProperty]
    private string _selectedProfile;

    [ObservableProperty]
    private string _tierText = "Hardware tier is not known yet.";

    [ObservableProperty]
    private string _statusMessage = "Choose a profile to preview recommendations. Choosing a profile does not write anything.";

    [ObservableProperty]
    private string _benchmarkMessage;

    [ObservableProperty]
    private bool _canApply;

    [ObservableProperty]
    private bool _isBusy;

    partial void OnSelectedProfileChanged(string value)
    {
        if (_suppress || !Enum.TryParse<OptimizationProfile>(value, out var profile))
        {
            return;
        }

        _selection.Current = profile;
        _ = LoadAsync();
    }

    partial void OnIsBusyChanged(bool value)
    {
        PreviewCommand.NotifyCanExecuteChanged();
        OptimizeNowCommand.NotifyCanExecuteChanged();
        UndoLastCommand.NotifyCanExecuteChanged();
    }

    partial void OnCanApplyChanged(bool value) => OptimizeNowCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task PreviewAsync()
    {
        if (!Enum.TryParse<OptimizationProfile>(SelectedProfile, out var profile))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var preview = await _optimization.PreviewAsync(profile);
            PreviewRows.Clear();
            if (!preview.Succeeded || preview.Value is null)
            {
                StatusMessage = preview.Error ?? "The preview could not be built.";
                OnPropertyChanged(nameof(HasPreview));
                return;
            }

            foreach (var edit in preview.Value.Edits)
            {
                PreviewRows.Add(new DiagnosticRowModel(
                    edit.Setting + "  ·  " + edit.Key,
                    edit.Path + "  ·  " + edit.CurrentRaw + " → " + edit.NewRaw,
                    "Change",
                    StatusKind.Attention));
            }

            CanApply = preview.Value.CanApply;
            StatusMessage = preview.Value.Edits.Count == 0
                ? preview.Value.BlockReason ?? "There is no applicable change for this profile."
                : preview.Value.Edits.Count.ToString(CultureInfo.InvariantCulture) + " value(s) would change. Nothing has been written.";
            OnPropertyChanged(nameof(HasPreview));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOptimize))]
    private async Task OptimizeNowAsync()
    {
        if (!Enum.TryParse<OptimizationProfile>(SelectedProfile, out var profile))
        {
            return;
        }

        var accepted = _confirm.Confirm(
            "Optimize now",
            "Apply the previewed " + profile + " changes to existing configuration keys? A backup is created first. Registry values are not written.");
        if (!accepted)
        {
            StatusMessage = "Optimization was cancelled.";
            _log.Write(LogSeverity.Information, "Optimize", "Optimization was cancelled.");
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _optimization.ApplyAsync(profile, confirmed: true);
            StatusMessage = result.Succeeded && result.Value is not null
                ? result.Value.Summary + " " + result.Value.ResultText + " Backup " + result.Value.BackupId + "."
                : result.Error ?? "Optimization could not be applied.";
            if (result.Succeeded)
            {
                _toasts.Show(ToastCatalog.OptimizationApplied);
            }
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task UndoLastAsync()
    {
        var accepted = _confirm.Confirm(
            "Undo optimization",
            "Restore the backup from the last optimization? GameLoop must be closed. Registry values are not written.");
        if (!accepted)
        {
            StatusMessage = "Undo was cancelled.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _optimization.UndoLastAsync(confirmed: true);
            StatusMessage = result.Succeeded
                ? "The last optimization was undone with backup " + result.Value?.BackupId + "."
                : result.Error ?? "The last optimization could not be undone.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRun() => !IsBusy;

    private bool CanOptimize() => !IsBusy && CanApply;

    public void Refresh()
    {
        _suppress = true;
        SelectedProfile = _selection.Current.ToString();
        _suppress = false;
        BenchmarkMessage = _benchmark.Describe().Error ?? "Benchmark mode is not implemented.";
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (!Enum.TryParse<OptimizationProfile>(SelectedProfile, out var profile))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var analysis = await _optimization.AnalyzeAsync(profile);
            Recommendations.Clear();
            if (!analysis.Succeeded || analysis.Value is null)
            {
                StatusMessage = analysis.Error ?? "Optimization could not be analyzed.";
                CanApply = false;
                return;
            }

            TierText = "Hardware tier: " + analysis.Value.Tier + (analysis.Value.GameLoopRunning ? ". GameLoop is running." : ".");
            foreach (var item in analysis.Value.Recommendations)
            {
                Recommendations.Add(new RecommendationRow(
                    item.Setting,
                    item.CurrentValue ?? "—",
                    item.RecommendedValue ?? "—",
                    item.Reason,
                    item.Risk.ToString(),
                    item.RequiresRestart ? "Yes" : "No",
                    item.Reversible ? "Yes" : "No",
                    StatusText(item.Status),
                    StatusKindFor(item.Status)));
            }

            var applicable = analysis.Value.Recommendations.Count(item => item.Status == RecommendationStatus.Applicable);
            CanApply = applicable > 0 && !analysis.Value.GameLoopRunning;
            if (analysis.Value.Notice is not null)
            {
                StatusMessage = analysis.Value.Notice;
            }
            else if (analysis.Value.GameLoopRunning)
            {
                StatusMessage = "GameLoop is running. Close it before optimizing. This action does not stop the process.";
            }
            else
            {
                StatusMessage = applicable.ToString(CultureInfo.InvariantCulture) + " applicable recommendation(s). Nothing has been written.";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string StatusText(RecommendationStatus status) => status switch
    {
        RecommendationStatus.Applicable => "Applicable",
        RecommendationStatus.AlreadyOptimal => "Already optimal",
        RecommendationStatus.NotSupported => "Not supported",
        RecommendationStatus.NotImplemented => "Not implemented",
        _ => "Skipped"
    };

    private static StatusKind StatusKindFor(RecommendationStatus status) => status switch
    {
        RecommendationStatus.Applicable => StatusKind.Attention,
        RecommendationStatus.AlreadyOptimal => StatusKind.Ready,
        RecommendationStatus.NotImplemented => StatusKind.Unavailable,
        _ => StatusKind.Neutral
    };
}

public sealed record RecommendationRow(
    string Setting,
    string Current,
    string Recommended,
    string Reason,
    string Risk,
    string Restart,
    string Reversible,
    string Status,
    StatusKind Kind);

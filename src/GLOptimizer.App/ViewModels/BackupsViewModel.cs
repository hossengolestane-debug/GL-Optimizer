using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.App.Services;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Backup;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Navigation;
using GLOptimizer.Core.Notifications;

namespace GLOptimizer.App.ViewModels;

public partial class BackupsViewModel : PageViewModel, IRefreshable
{
    private readonly IBackupService _backups;
    private readonly ILogStore _log;
    private readonly IUserConfirmation _confirm;
    private readonly IToastCenter _toasts;

    public BackupsViewModel(IBackupService backups, ILogStore log, IUserConfirmation confirm, IToastCenter toasts)
        : base(AppPage.Backups)
    {
        _backups = backups;
        _log = log;
        _confirm = confirm;
        _toasts = toasts;
    }

    public ObservableCollection<BackupRow> Items { get; } = new();

    public ObservableCollection<DiagnosticRowModel> PreviewRows { get; } = new();

    public bool HasBackups => Items.Count > 0;

    public bool HasPreview => PreviewRows.Count > 0;

    public string SafetyNote => Phase0Notices.BackupSafety;

    [ObservableProperty]
    private string _reason = string.Empty;

    [ObservableProperty]
    private string _emptyMessage = "No backups yet.";

    [ObservableProperty]
    private string _statusMessage = "Create a backup of the configuration files discovery marked Found.";

    [ObservableProperty]
    private string _detailsText = "Choose Details to read a manifest.";

    [ObservableProperty]
    private string? _previewBackupId;

    [ObservableProperty]
    private bool _canConfirmRestore;

    [ObservableProperty]
    private bool _isBusy;

    partial void OnReasonChanged(string value) => CreateBackupCommand.NotifyCanExecuteChanged();

    partial void OnIsBusyChanged(bool value)
    {
        CreateBackupCommand.NotifyCanExecuteChanged();
        ShowDetailsCommand.NotifyCanExecuteChanged();
        PreviewRestoreCommand.NotifyCanExecuteChanged();
        ConfirmRestoreCommand.NotifyCanExecuteChanged();
        DeleteBackupCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateBackupAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _backups.CreateAsync(Reason);
            StatusMessage = result.Succeeded
                ? "Created backup " + result.Value?.Id + "."
                : result.Error ?? "The backup could not be created.";
            if (result.Succeeded)
            {
                Reason = string.Empty;
                _toasts.Show(ToastCatalog.BackupCreated);
            }

            Refresh();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanCreate() => !IsBusy && !string.IsNullOrWhiteSpace(Reason);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void ShowDetails(string? backupId)
    {
        if (string.IsNullOrWhiteSpace(backupId))
        {
            return;
        }

        var result = _backups.Get(backupId);
        if (!result.Succeeded || result.Value is null)
        {
            DetailsText = result.Error ?? "The backup could not be read.";
            return;
        }

        DetailsText = Describe(result.Value);
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task PreviewRestoreAsync(string? backupId)
    {
        if (string.IsNullOrWhiteSpace(backupId))
        {
            return;
        }

        IsBusy = true;
        try
        {
            PreviewRows.Clear();
            PreviewBackupId = null;
            CanConfirmRestore = false;
            var result = await _backups.PreviewRestoreAsync(backupId);
            if (!result.Succeeded || result.Value is null)
            {
                StatusMessage = result.Error ?? "The dry run could not be completed.";
                OnPropertyChanged(nameof(HasPreview));
                return;
            }

            var preview = result.Value;
            PreviewBackupId = preview.BackupId;
            foreach (var file in preview.Files)
            {
                var badge = file.Disposition switch
                {
                    RestoreDisposition.Replace => "Replace",
                    RestoreDisposition.Unchanged => "Unchanged",
                    _ => "Skip"
                };
                var kind = file.Disposition switch
                {
                    RestoreDisposition.Replace => StatusKind.Attention,
                    RestoreDisposition.Unchanged => StatusKind.Ready,
                    _ => StatusKind.Unavailable
                };
                var hashes = "Backup " + (file.BackupSha256 ?? "Unknown")
                    + "  ·  Current " + (file.CurrentSha256 ?? "missing");
                PreviewRows.Add(new DiagnosticRowModel(file.OriginalPath, file.Detail + "  ·  " + hashes, badge, kind));
            }

            CanConfirmRestore = preview.CanRestore;
            StatusMessage = preview.Warning ?? "Dry run finished. Confirm to replace the listed files.";
            OnPropertyChanged(nameof(HasPreview));
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmRestoreAsync()
    {
        if (string.IsNullOrWhiteSpace(PreviewBackupId) || !CanConfirmRestore)
        {
            return;
        }

        var id = PreviewBackupId;
        var accepted = _confirm.Confirm(
            "Restore backup",
            "Replace the files listed in the dry run for backup " + id
            + "? A pre-restore backup of the current files is created first. Registry values are not written.");
        if (!accepted)
        {
            StatusMessage = "Restore was cancelled.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _backups.RestoreAsync(id, confirmed: true);
            StatusMessage = result.Succeeded
                ? "Restored " + id + ". Pre-restore backup: " + (result.Value?.PreRestoreBackupId ?? "none") + "."
                : result.Error ?? "The backup could not be restored.";
            Refresh();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanConfirm() => !IsBusy && CanConfirmRestore && !string.IsNullOrWhiteSpace(PreviewBackupId);

    partial void OnCanConfirmRestoreChanged(bool value) => ConfirmRestoreCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task DeleteBackupAsync(string? backupId)
    {
        if (string.IsNullOrWhiteSpace(backupId))
        {
            return;
        }

        var accepted = _confirm.Confirm(
            "Delete backup",
            "Delete backup " + backupId + "? This removes only the copy under GL Optimizer backups. GameLoop files are not deleted.");
        if (!accepted)
        {
            StatusMessage = "Delete was cancelled.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _backups.DeleteAsync(backupId, confirmed: true);
            StatusMessage = result.Succeeded
                ? "Deleted backup " + backupId + "."
                : result.Error ?? "The backup could not be deleted.";
            if (result.Succeeded && string.Equals(PreviewBackupId, backupId, StringComparison.Ordinal))
            {
                PreviewRows.Clear();
                PreviewBackupId = null;
                CanConfirmRestore = false;
                DetailsText = "Choose Details to read a manifest.";
                OnPropertyChanged(nameof(HasPreview));
            }

            Refresh();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRun() => !IsBusy;

    public void Refresh()
    {
        Items.Clear();
        var listed = _backups.List();
        if (listed.Succeeded && listed.Value is not null)
        {
            foreach (var record in listed.Value)
            {
                var when = record.CreatedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)
                    + "  ·  "
                    + record.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
                Items.Add(new BackupRow(
                    record.Id,
                    record.Damaged ? "Damaged backup" : record.Label,
                    when,
                    record.SizeBytes.ToString(CultureInfo.InvariantCulture) + " bytes",
                    record.Damaged ? "Damaged" : "Stored",
                    record.Damaged ? StatusKind.Attention : StatusKind.Ready));
            }

            EmptyMessage = "No backups yet.";
        }
        else
        {
            EmptyMessage = listed.Error ?? "Backups could not be listed.";
        }

        OnPropertyChanged(nameof(HasBackups));
    }

    private static string Describe(BackupDetails details)
    {
        if (details.Damaged || details.Manifest is null)
        {
            return "Damaged backup " + details.Id + ". " + (details.Problem ?? "The manifest could not be read.");
        }

        var manifest = details.Manifest;
        var text = new StringBuilder();
        text.Append("Backup ").Append(manifest.Id).AppendLine();
        text.Append("UTC ").Append(manifest.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture));
        text.Append("  ·  Local ").Append(manifest.CreatedAtLocal).AppendLine();
        text.Append("Reason ").Append(manifest.Reason).AppendLine();
        text.Append("GameLoop version ").Append(string.IsNullOrWhiteSpace(manifest.GameLoopVersion) ? "Unknown" : manifest.GameLoopVersion).AppendLine();
        text.AppendLine("Settings");
        text.Append("Renderer ").Append(manifest.Settings.Renderer ?? "Unknown");
        text.Append("  ·  Resolution ").Append(manifest.Settings.Resolution ?? "Unknown");
        text.Append("  ·  DPI ").Append(manifest.Settings.Dpi ?? "Unknown").AppendLine();
        text.Append("Memory ").Append(manifest.Settings.MemoryAllocation ?? "Unknown");
        text.Append("  ·  CPU ").Append(manifest.Settings.CpuAllocation ?? "Unknown");
        text.Append("  ·  VSync ").Append(manifest.Settings.VSync ?? "Unknown").AppendLine();
        text.Append("Anti-aliasing ").Append(manifest.Settings.AntiAliasing ?? "Unknown");
        text.Append("  ·  FPS ").Append(manifest.Settings.FpsTarget ?? "Unknown").AppendLine();
        text.AppendLine("Files");
        foreach (var file in manifest.Files)
        {
            text.Append(file.OriginalPath).Append("  →  ").Append(file.StoredName);
            text.Append("  ·  ").Append(file.Sha256);
            text.Append("  ·  ").Append(file.SizeBytes.ToString(CultureInfo.InvariantCulture)).AppendLine(" bytes");
        }

        text.AppendLine("Registry values (text only, not written back)");
        if (manifest.RegistryValues.Count == 0)
        {
            text.AppendLine("None.");
        }

        foreach (var pair in manifest.RegistryValues.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            text.Append(pair.Key).Append('=').AppendLine(pair.Value);
        }

        if (manifest.UnmappedRegistryKeys.Count > 0)
        {
            text.Append("Unmapped renderer values: ").AppendLine(string.Join(", ", manifest.UnmappedRegistryKeys));
        }

        return text.ToString();
    }
}

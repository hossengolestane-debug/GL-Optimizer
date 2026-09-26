using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Navigation;

namespace GLOptimizer.App.ViewModels;

public partial class BackupsViewModel : PageViewModel, IRefreshable
{
    private readonly IBackupService _backups;
    private readonly ILogStore _log;

    public BackupsViewModel(IBackupService backups, ILogStore log)
        : base(AppPage.Backups)
    {
        _backups = backups;
        _log = log;
    }

    public ObservableCollection<BackupRow> Items { get; } = new();

    public bool HasBackups => Items.Count > 0;

    public string SafetyNote => Phase0Notices.NoBackup;

    [ObservableProperty]
    private string _emptyMessage = Phase0Notices.NoBackup;

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private void CreateBackup()
    {
        _log.Write(LogSeverity.Warning, "Backups", "Create backup was requested, but backups are not implemented.");
    }

    private static bool CanCreate() => false;

    public void Refresh()
    {
        Items.Clear();
        var listed = _backups.List();
        if (listed.Succeeded && listed.Value is not null)
        {
            foreach (var record in listed.Value)
            {
                Items.Add(new BackupRow(
                    record.Label,
                    record.CreatedAt.ToLocalTime().ToString("g"),
                    $"{record.SizeBytes} bytes",
                    record.Id,
                    StatusKind.Neutral));
            }
        }

        EmptyMessage = listed.Succeeded
            ? "No backups yet."
            : listed.Error ?? Phase0Notices.NoBackup;
        OnPropertyChanged(nameof(HasBackups));
    }
}

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Navigation;

namespace GLOptimizer.App.ViewModels;

public partial class LogsViewModel : PageViewModel, IRefreshable
{
    private readonly ILogStore _log;
    private readonly IFolderOpener _folders;

    public LogsViewModel(ILogStore log, IFolderOpener folders)
        : base(AppPage.Logs)
    {
        _log = log;
        _folders = folders;
        foreach (var level in Enum.GetValues<LogSeverity>())
        {
            Levels.Add(new ChoiceItemViewModel
            {
                Label = level.ToString(),
                Level = level,
                IsSelected = level == LogSeverity.Trace
            });
        }
    }

    public ObservableCollection<ChoiceItemViewModel> Levels { get; } = new();

    public ObservableCollection<LogRow> Entries { get; } = new();

    public string LogPath => _log.ActiveLogFilePath;

    public bool HasEntries => Entries.Count > 0;

    [ObservableProperty]
    private LogSeverity _filter = LogSeverity.Trace;

    [ObservableProperty]
    private string? _statusMessage;

    [RelayCommand]
    private void SelectLevel(LogSeverity level)
    {
        Filter = level;
        foreach (var item in Levels)
        {
            item.IsSelected = item.Level == level;
        }

        Refresh();
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        var result = _folders.Open(_log.LogDirectory);
        StatusMessage = result.Succeeded ? null : result.Error;
    }

    [RelayCommand]
    private void Reload() => Refresh();

    public void Refresh()
    {
        Entries.Clear();
        foreach (var entry in _log.ReadActiveLog(500).Reverse())
        {
            if (entry.Severity < Filter)
            {
                continue;
            }

            Entries.Add(new LogRow(
                entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture),
                entry.Severity.ToString(),
                entry.Category,
                entry.Message,
                entry.Exception,
                KindFor(entry.Severity)));
        }

        StatusMessage = _log.LastError;
        OnPropertyChanged(nameof(HasEntries));
    }

    private static StatusKind KindFor(LogSeverity severity) => severity switch
    {
        LogSeverity.Error or LogSeverity.Critical or LogSeverity.Warning => StatusKind.Attention,
        LogSeverity.Information => StatusKind.Ready,
        _ => StatusKind.Neutral
    };
}

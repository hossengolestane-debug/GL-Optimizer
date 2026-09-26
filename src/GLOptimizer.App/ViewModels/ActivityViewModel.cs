using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Navigation;

namespace GLOptimizer.App.ViewModels;

public partial class ActivityViewModel : PageViewModel, IRefreshable
{
    private readonly ILogStore _log;

    public ActivityViewModel(ILogStore log)
        : base(AppPage.Activity)
    {
        ArgumentNullException.ThrowIfNull(log);
        _log = log;
    }

    public ObservableCollection<ActivityRow> Entries { get; } = new();

    public bool HasEntries => Entries.Count > 0;

    [ObservableProperty]
    private string _selectedTime = string.Empty;

    [ObservableProperty]
    private string _selectedTitle = "Select an entry.";

    [ObservableProperty]
    private string _selectedDetail = "Details appear here. Paths replace the user profile with %USERPROFILE%.";

    [RelayCommand]
    private void Select(ActivityRow? row)
    {
        if (row is null)
        {
            return;
        }

        SelectedTime = row.Time;
        SelectedTitle = row.Title;
        SelectedDetail = row.Detail;
    }

    [RelayCommand]
    private void Reload() => Refresh();

    public void Refresh()
    {
        Entries.Clear();
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var entry in _log.GetRecent(200).Reverse())
        {
            Entries.Add(new ActivityRow(
                entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture),
                entry.Category,
                ReportRedaction.Redact(entry.Message, profile)));
        }

        OnPropertyChanged(nameof(HasEntries));
    }
}

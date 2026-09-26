using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.Core;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Navigation;
using GLOptimizer.Core.Settings;
using GLOptimizer.Infrastructure;

namespace GLOptimizer.App.ViewModels;

public partial class SettingsViewModel : PageViewModel
{
    private readonly ISettingsStore _store;
    private readonly ILogStore _log;
    private readonly IMonitoringCoordinator _monitoring;

    public SettingsViewModel(ISettingsStore store, ILogStore log, AppDataLocations locations, IMonitoringCoordinator monitoring)
        : base(AppPage.Settings)
    {
        _store = store;
        _log = log;
        _monitoring = monitoring;
        PathsSummary = $"App data{Environment.NewLine}{locations.Root}{Environment.NewLine}{Environment.NewLine}Settings{Environment.NewLine}{locations.SettingsFile}{Environment.NewLine}{Environment.NewLine}Log{Environment.NewLine}{locations.ActiveLogFile}";
        var current = store.Current;
        MinimumLogLevel = current.MinimumLogLevel;
        RetentionDaysText = current.LogRetentionDays.ToString(CultureInfo.InvariantCulture);
        MaxLogMegabytesText = Math.Max(1, current.MaxLogFileBytes / (1024 * 1024)).ToString(CultureInfo.InvariantCulture);
        SampleIntervalMilliseconds = AppSettingsRules.NormalizeSampleInterval(current.SampleIntervalMilliseconds);
        foreach (var level in Enum.GetValues<LogSeverity>())
        {
            Levels.Add(new ChoiceItemViewModel
            {
                Label = level.ToString(),
                Level = level,
                IsSelected = level == MinimumLogLevel
            });
        }
    }

    public ObservableCollection<ChoiceItemViewModel> Levels { get; } = new();

    public string Appearance => "Monochrome";

    public string PathsSummary { get; }

    public bool ShowDeveloperTools => BuildInfo.IsDeveloperMode;

    [ObservableProperty]
    private LogSeverity _minimumLogLevel;

    [ObservableProperty]
    private string _retentionDaysText;

    [ObservableProperty]
    private string _maxLogMegabytesText;

    [ObservableProperty]
    private int _sampleIntervalMilliseconds;

    [ObservableProperty]
    private string? _statusMessage;

    [RelayCommand]
    private void SelectLevel(LogSeverity level)
    {
        MinimumLogLevel = level;
        foreach (var item in Levels)
        {
            item.IsSelected = item.Level == level;
        }
    }

    [RelayCommand]
    private void SelectInterval(string milliseconds)
    {
        if (int.TryParse(milliseconds, System.Globalization.NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            SampleIntervalMilliseconds = AppSettingsRules.NormalizeSampleInterval(value);
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (!int.TryParse(RetentionDaysText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days)
            || days is < AppSettingsRules.MinRetentionDays or > AppSettingsRules.MaxRetentionDays)
        {
            StatusMessage = $"Retention must be a whole number of days from {AppSettingsRules.MinRetentionDays} to {AppSettingsRules.MaxRetentionDays}.";
            return;
        }

        if (!int.TryParse(MaxLogMegabytesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var megabytes)
            || megabytes is < 1 or > 50)
        {
            StatusMessage = "Log size must be a whole number of megabytes from 1 to 50.";
            return;
        }

        var settings = _store.Current;
        settings.MinimumLogLevel = MinimumLogLevel;
        settings.LogRetentionDays = days;
        settings.MaxLogFileBytes = megabytes * 1024L * 1024L;
        settings.SampleIntervalMilliseconds = SampleIntervalMilliseconds;
        var result = _store.Save(settings);
        if (!result.Succeeded)
        {
            StatusMessage = result.Error;
            return;
        }

        var saved = _store.Current;
        _log.ApplyPolicy(saved);
        SampleIntervalMilliseconds = saved.SampleIntervalMilliseconds;
        _monitoring.NotifyIntervalChanged();
        RetentionDaysText = saved.LogRetentionDays.ToString(CultureInfo.InvariantCulture);
        MaxLogMegabytesText = Math.Max(1, saved.MaxLogFileBytes / (1024 * 1024)).ToString(CultureInfo.InvariantCulture);
        StatusMessage = "Settings saved.";
        _log.Write(LogSeverity.Information, "Settings", "Settings saved.");
    }

    [RelayCommand(CanExecute = nameof(CanUseDeveloperTools))]
    private void WriteDeveloperLog()
    {
        if (!BuildInfo.IsDeveloperMode)
        {
            return;
        }

        _log.Write(LogSeverity.Debug, "Developer", "Developer mode log entry.");
        StatusMessage = "Wrote a debug log entry.";
    }

    private static bool CanUseDeveloperTools() => BuildInfo.IsDeveloperMode;
}

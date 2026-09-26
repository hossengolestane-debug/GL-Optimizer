using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.App.Services;
using GLOptimizer.Core;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Navigation;
using GLOptimizer.Core.Notifications;
using GLOptimizer.Core.Settings;
using GLOptimizer.Infrastructure;

namespace GLOptimizer.App.ViewModels;

public partial class SettingsViewModel : PageViewModel
{
    private readonly ISettingsStore _store;
    private readonly ILogStore _log;
    private readonly IMonitoringCoordinator _monitoring;
    private readonly IStartupRegistration _startup;
    private readonly IUpdateService _updates;
    private readonly IBackupService _backups;
    private readonly IToastCenter _toasts;

    public SettingsViewModel(
        ISettingsStore store,
        ILogStore log,
        AppDataLocations locations,
        IMonitoringCoordinator monitoring,
        IStartupRegistration startup,
        IUpdateService updates,
        IBackupService backups,
        IToastCenter toasts)
        : base(AppPage.Settings)
    {
        _store = store;
        _log = log;
        _monitoring = monitoring;
        _startup = startup;
        _updates = updates;
        _backups = backups;
        _toasts = toasts;
        PathsSummary = $"App data{Environment.NewLine}{locations.Root}{Environment.NewLine}{Environment.NewLine}Settings{Environment.NewLine}{locations.SettingsFile}{Environment.NewLine}{Environment.NewLine}Log{Environment.NewLine}{locations.ActiveLogFile}";
        var current = store.Current;
        MinimumLogLevel = current.MinimumLogLevel;
        RetentionDaysText = current.LogRetentionDays.ToString(CultureInfo.InvariantCulture);
        MaxLogMegabytesText = Math.Max(1, current.MaxLogFileBytes / (1024 * 1024)).ToString(CultureInfo.InvariantCulture);
        SampleIntervalMilliseconds = AppSettingsRules.NormalizeSampleInterval(current.SampleIntervalMilliseconds);
        StartWithWindows = current.StartWithWindows;
        MinimizeToTray = current.MinimizeToTray;
        AutomaticBackup = current.AutomaticBackup;
        MonitoringEnabled = current.MonitoringEnabled;
        GameLoopPathOverride = current.GameLoopPathOverride ?? string.Empty;
        Theme = current.Theme;
        DeveloperSimulation = current.DeveloperSimulationEnabled && BuildInfo.IsDeveloperMode;
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

    public string ThemeNote => ThemePolicy.LightNotImplemented;

    public string StartupNote => _startup.IsSupported
        ? "Writes or removes only the HKCU Run value named GL Optimizer."
        : "Start with Windows is only available on Windows.";

    [ObservableProperty]
    private LogSeverity _minimumLogLevel;

    [ObservableProperty]
    private string _retentionDaysText;

    [ObservableProperty]
    private string _maxLogMegabytesText;

    [ObservableProperty]
    private int _sampleIntervalMilliseconds;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private bool _automaticBackup;

    [ObservableProperty]
    private bool _monitoringEnabled;

    [ObservableProperty]
    private string _gameLoopPathOverride = string.Empty;

    [ObservableProperty]
    private AppTheme _theme = AppTheme.Dark;

    [ObservableProperty]
    private bool _developerSimulation;

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
    private async Task SaveAsync()
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

        var overrideError = InstallOverrideRules.Validate(GameLoopPathOverride);
        if (overrideError is not null)
        {
            StatusMessage = overrideError;
            return;
        }

        if (StartWithWindows)
        {
            var startup = StartupRegistrationRules.Apply(_startup, true, Environment.ProcessPath ?? string.Empty);
            if (!startup.Succeeded)
            {
                StatusMessage = startup.Error;
                return;
            }
        }
        else
        {
            var removed = StartupRegistrationRules.Apply(_startup, false, string.Empty);
            if (!removed.Succeeded)
            {
                StatusMessage = removed.Error;
                return;
            }
        }

        var settings = _store.Current;
        settings.MinimumLogLevel = MinimumLogLevel;
        settings.LogRetentionDays = days;
        settings.MaxLogFileBytes = megabytes * 1024L * 1024L;
        settings.SampleIntervalMilliseconds = SampleIntervalMilliseconds;
        settings.StartWithWindows = StartWithWindows;
        settings.MinimizeToTray = MinimizeToTray;
        settings.AutomaticBackup = AutomaticBackup;
        settings.MonitoringEnabled = MonitoringEnabled;
        settings.GameLoopPathOverride = string.IsNullOrWhiteSpace(GameLoopPathOverride) ? null : GameLoopPathOverride.Trim();
        settings.Theme = Theme;
        settings.DeveloperSimulationEnabled = BuildInfo.IsDeveloperMode && DeveloperSimulation;
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
        _monitoring.Refresh();
        if (saved.AutomaticBackup)
        {
            var backup = await _backups.CreateAsync("Automatic backup");
            if (!backup.Succeeded && backup.Error is not null && !backup.Error.Contains("No discovered", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "Settings saved. Automatic backup did not finish. " + backup.Error;
                return;
            }
        }

        if (saved.Theme == AppTheme.Light)
        {
            StatusMessage = ThemePolicy.LightNotImplemented;
        }
        RetentionDaysText = saved.LogRetentionDays.ToString(CultureInfo.InvariantCulture);
        MaxLogMegabytesText = Math.Max(1, saved.MaxLogFileBytes / (1024 * 1024)).ToString(CultureInfo.InvariantCulture);
        if (StatusMessage != ThemePolicy.LightNotImplemented)
        {
            StatusMessage = "Settings saved.";
        }

        _log.Write(LogSeverity.Information, "Settings", "Settings saved.");
        _toasts.Show(ToastCatalog.SettingsSaved);
    }

    [RelayCommand]
    private async Task CheckUpdatesAsync()
    {
        var result = await _updates.CheckAsync();
        StatusMessage = result.Error ?? "No update was reported.";
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

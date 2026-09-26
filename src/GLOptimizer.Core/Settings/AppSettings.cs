using GLOptimizer.Core.Logging;

namespace GLOptimizer.Core.Settings;

public enum AppTheme
{
    Dark = 0,
    Light = 1
}

public sealed class AppSettings
{
    public bool SidebarCollapsed { get; set; }

    public LogSeverity MinimumLogLevel { get; set; } = LogSeverity.Information;

    public int LogRetentionDays { get; set; } = AppSettingsRules.DefaultRetentionDays;

    public long MaxLogFileBytes { get; set; } = AppSettingsRules.DefaultMaxLogFileBytes;

    public int SampleIntervalMilliseconds { get; set; } = AppSettingsRules.DefaultSampleIntervalMilliseconds;

    public bool StartWithWindows { get; set; }

    public bool MinimizeToTray { get; set; }

    public bool AutomaticBackup { get; set; }

    public bool MonitoringEnabled { get; set; } = true;

    public string? GameLoopPathOverride { get; set; }

    public AppTheme Theme { get; set; } = AppTheme.Dark;

    public bool FirstRunCompleted { get; set; }

    public bool DeveloperSimulationEnabled { get; set; }

    public AppSettings Copy() => new()
    {
        SidebarCollapsed = SidebarCollapsed,
        MinimumLogLevel = MinimumLogLevel,
        LogRetentionDays = LogRetentionDays,
        MaxLogFileBytes = MaxLogFileBytes,
        SampleIntervalMilliseconds = SampleIntervalMilliseconds,
        StartWithWindows = StartWithWindows,
        MinimizeToTray = MinimizeToTray,
        AutomaticBackup = AutomaticBackup,
        MonitoringEnabled = MonitoringEnabled,
        GameLoopPathOverride = GameLoopPathOverride,
        Theme = Theme,
        FirstRunCompleted = FirstRunCompleted,
        DeveloperSimulationEnabled = DeveloperSimulationEnabled
    };
}

public static class AppSettingsRules
{
    public const int DefaultRetentionDays = 14;
    public const int MinRetentionDays = 1;
    public const int MaxRetentionDays = 90;
    public const long DefaultMaxLogFileBytes = 2 * 1024 * 1024;
    public const long MinMaxLogFileBytes = 64 * 1024;
    public const long MaxMaxLogFileBytes = 50L * 1024 * 1024;
    public const int DefaultSampleIntervalMilliseconds = 1000;

    public static int ClampRetentionDays(int days) => Math.Clamp(days, MinRetentionDays, MaxRetentionDays);

    public static int NormalizeSampleInterval(int milliseconds) =>
        milliseconds is 500 or 1000 or 2000 ? milliseconds : DefaultSampleIntervalMilliseconds;

    public static long ClampMaxLogFileBytes(long bytes) => Math.Clamp(bytes, MinMaxLogFileBytes, MaxMaxLogFileBytes);

    public static void Normalize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.LogRetentionDays = ClampRetentionDays(settings.LogRetentionDays);
        settings.MaxLogFileBytes = ClampMaxLogFileBytes(settings.MaxLogFileBytes);
        settings.SampleIntervalMilliseconds = NormalizeSampleInterval(settings.SampleIntervalMilliseconds);
        if (!Enum.IsDefined(settings.MinimumLogLevel))
        {
            settings.MinimumLogLevel = LogSeverity.Information;
        }

        if (!Enum.IsDefined(settings.Theme))
        {
            settings.Theme = AppTheme.Dark;
        }

        settings.GameLoopPathOverride = string.IsNullOrWhiteSpace(settings.GameLoopPathOverride)
            ? null
            : settings.GameLoopPathOverride.Trim();
#if !DEBUG
        settings.DeveloperSimulationEnabled = false;
#endif
    }
}

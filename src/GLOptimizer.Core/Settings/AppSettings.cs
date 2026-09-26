using GLOptimizer.Core.Logging;

namespace GLOptimizer.Core.Settings;

public sealed class AppSettings
{
    public bool SidebarCollapsed { get; set; }

    public LogSeverity MinimumLogLevel { get; set; } = LogSeverity.Information;

    public int LogRetentionDays { get; set; } = AppSettingsRules.DefaultRetentionDays;

    public long MaxLogFileBytes { get; set; } = AppSettingsRules.DefaultMaxLogFileBytes;

    public int SampleIntervalMilliseconds { get; set; } = AppSettingsRules.DefaultSampleIntervalMilliseconds;

    public AppSettings Copy() => new()
    {
        SidebarCollapsed = SidebarCollapsed,
        MinimumLogLevel = MinimumLogLevel,
        LogRetentionDays = LogRetentionDays,
        MaxLogFileBytes = MaxLogFileBytes,
        SampleIntervalMilliseconds = SampleIntervalMilliseconds
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
    }
}

using GLOptimizer.Core.Logging;

namespace GLOptimizer.Core.Settings;

public sealed class AppSettings
{
    public bool SidebarCollapsed { get; set; }

    public LogSeverity MinimumLogLevel { get; set; } = LogSeverity.Information;

    public int LogRetentionDays { get; set; } = AppSettingsRules.DefaultRetentionDays;

    public long MaxLogFileBytes { get; set; } = AppSettingsRules.DefaultMaxLogFileBytes;

    public AppSettings Copy() => new()
    {
        SidebarCollapsed = SidebarCollapsed,
        MinimumLogLevel = MinimumLogLevel,
        LogRetentionDays = LogRetentionDays,
        MaxLogFileBytes = MaxLogFileBytes
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

    public static int ClampRetentionDays(int days) => Math.Clamp(days, MinRetentionDays, MaxRetentionDays);

    public static long ClampMaxLogFileBytes(long bytes) => Math.Clamp(bytes, MinMaxLogFileBytes, MaxMaxLogFileBytes);

    public static void Normalize(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings.LogRetentionDays = ClampRetentionDays(settings.LogRetentionDays);
        settings.MaxLogFileBytes = ClampMaxLogFileBytes(settings.MaxLogFileBytes);
        if (!Enum.IsDefined(settings.MinimumLogLevel))
        {
            settings.MinimumLogLevel = LogSeverity.Information;
        }
    }
}

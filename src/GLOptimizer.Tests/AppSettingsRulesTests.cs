using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Settings;

namespace GLOptimizer.Tests;

public class AppSettingsRulesTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(14, 14)]
    [InlineData(400, 90)]
    public void Retention_is_clamped(int input, int expected)
    {
        Assert.Equal(expected, AppSettingsRules.ClampRetentionDays(input));
    }

    [Fact]
    public void Normalize_repairs_an_undefined_log_level()
    {
        var settings = new AppSettings
        {
            MinimumLogLevel = (LogSeverity)42,
            LogRetentionDays = 0,
            MaxLogFileBytes = 10
        };

        AppSettingsRules.Normalize(settings);

        Assert.Equal(LogSeverity.Information, settings.MinimumLogLevel);
        Assert.Equal(AppSettingsRules.MinRetentionDays, settings.LogRetentionDays);
        Assert.Equal(AppSettingsRules.MinMaxLogFileBytes, settings.MaxLogFileBytes);
    }

    [Fact]
    public void Copy_is_independent()
    {
        var settings = new AppSettings { SidebarCollapsed = true, LogRetentionDays = 7 };
        var copy = settings.Copy();
        copy.SidebarCollapsed = false;

        Assert.True(settings.SidebarCollapsed);
        Assert.Equal(7, copy.LogRetentionDays);
    }
}

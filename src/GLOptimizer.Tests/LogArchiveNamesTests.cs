using GLOptimizer.Core.Logging;

namespace GLOptimizer.Tests;

public class LogArchiveNamesTests
{
    [Fact]
    public void Stamp_roundtrips_from_the_file_name()
    {
        var when = new DateTimeOffset(2026, 9, 26, 16, 41, 2, 123, TimeSpan.Zero);
        var name = LogArchiveNames.Create(when);

        Assert.True(LogArchiveNames.TryReadStamp(name, out var stamp));
        Assert.Equal(when, stamp);
    }

    [Fact]
    public void Suffixed_archive_uses_the_same_stamp()
    {
        var when = new DateTimeOffset(2026, 9, 26, 1, 2, 3, 4, TimeSpan.Zero);
        var name = LogArchiveNames.Create(when, suffix: 2);

        Assert.EndsWith("-2.log", name, StringComparison.Ordinal);
        Assert.True(LogArchiveNames.TryReadStamp(name, out var stamp));
        Assert.Equal(when, stamp);
    }

    [Fact]
    public void Active_log_and_fresh_archives_are_not_expired()
    {
        var now = new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero);

        Assert.False(LogArchiveNames.IsExpired("gloptimizer.log", now, 14));
        Assert.False(LogArchiveNames.IsExpired(LogArchiveNames.Create(now.AddDays(-1)), now, 14));
        Assert.True(LogArchiveNames.IsExpired(LogArchiveNames.Create(now.AddDays(-30)), now, 14));
    }
}

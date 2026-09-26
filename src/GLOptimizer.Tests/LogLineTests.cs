using GLOptimizer.Core.Logging;

namespace GLOptimizer.Tests;

public class LogLineTests
{
    [Fact]
    public void Roundtrip_preserves_tabs_and_newlines()
    {
        var entry = new LogEntry(
            new DateTimeOffset(2026, 9, 26, 16, 41, 2, 123, TimeSpan.Zero),
            LogSeverity.Error,
            "App\tCore",
            "line1\nline2",
            "boom\r\nstack");

        var line = LogLine.Format(entry);
        Assert.DoesNotContain("\n", line);
        Assert.True(LogLine.TryParse(line, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(entry.Timestamp, parsed!.Timestamp);
        Assert.Equal(entry.Severity, parsed.Severity);
        Assert.Equal(entry.Category, parsed.Category);
        Assert.Equal(entry.Message, parsed.Message);
        Assert.Equal(entry.Exception, parsed.Exception);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a log")]
    [InlineData("2026-09-26T00:00:00.0000000+00:00\tNope\tApp\tmsg\t")]
    public void Parse_rejects_malformed_lines(string line)
    {
        Assert.False(LogLine.TryParse(line, out var parsed));
        Assert.Null(parsed);
    }
}

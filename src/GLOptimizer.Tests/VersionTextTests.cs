using GLOptimizer.Core.Detection;

namespace GLOptimizer.Tests;

public class VersionTextTests
{
    [Theory]
    [InlineData("1.2.3.4", "1.2.3.4")]
    [InlineData("v1.2", "1.2")]
    [InlineData("V10.0.1 extra", "10.0.1")]
    [InlineData("\"1.0.0\"", "1.0.0")]
    public void Numeric_versions_are_kept(string raw, string expected)
    {
        Assert.Equal(expected, VersionText.Normalize(raw));
    }

    [Theory]
    [InlineData("latest")]
    [InlineData("1")]
    [InlineData("1.2.3.4.5")]
    [InlineData("")]
    [InlineData(null)]
    public void Non_versions_are_rejected(string? raw)
    {
        Assert.Null(VersionText.Normalize(raw));
    }

    [Fact]
    public void Content_reads_known_keys_and_rejects_conflicts()
    {
        Assert.Equal("3.2.1", VersionText.FromContent("versionName=3.2.1\n"));
        Assert.Equal("1.0.4", VersionText.FromContent("{\n  \"Version\": \"1.0.4\"\n}\n"));
        Assert.Equal("2.1", VersionText.FromContent("AppVersion: 2.1\nName=Game\n"));
        Assert.Null(VersionText.FromContent("versionName=1.0.0\nVersion=2.0.0\n"));
        Assert.Null(VersionText.FromContent("latest\n"));
        Assert.Equal("1.2.3", VersionText.FromContent("versionName=1.2.3\nVersion=1.2.3\n"));
    }
}

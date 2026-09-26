using GLOptimizer.Core;

namespace GLOptimizer.Tests;

public class BuildInfoTests
{
    [Fact]
    public void Version_is_the_phase_0_release()
    {
        Assert.Equal("GL Optimizer", BuildInfo.ProductName);
        Assert.StartsWith("0.1.0", BuildInfo.Version, StringComparison.Ordinal);
    }

    [Fact]
    public void Developer_mode_is_compiled_only_for_debug()
    {
#if DEBUG
        Assert.True(BuildInfo.IsDeveloperMode);
#else
        Assert.False(BuildInfo.IsDeveloperMode);
#endif
    }
}

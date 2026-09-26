using GLOptimizer.Core.Diagnostics;

namespace GLOptimizer.Tests;

public class UserFacingErrorTests
{
    [Fact]
    public void Messages_follow_the_exception_type()
    {
        Assert.Equal(UserFacingError.Permission, UserFacingError.From(new UnauthorizedAccessException("denied")));
        Assert.Equal(UserFacingError.MissingPath, UserFacingError.From(new FileNotFoundException("missing")));
        Assert.Equal(UserFacingError.MissingPath, UserFacingError.From(new DirectoryNotFoundException("missing")));
        Assert.Equal(UserFacingError.FileAccess, UserFacingError.From(new IOException("busy")));
        Assert.Equal(UserFacingError.Unexpected, UserFacingError.From(new InvalidOperationException("x")));
    }
}

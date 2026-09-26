using GLOptimizer.Core.Results;

namespace GLOptimizer.Tests;

public class OperationResultTests
{
    [Fact]
    public void Success_has_no_error()
    {
        var result = OperationResult.Success();

        Assert.True(result.Succeeded);
        Assert.Equal(OperationStatus.Success, result.Status);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Failure_requires_a_message()
    {
        var result = OperationResult.Failure("disk full");

        Assert.False(result.Succeeded);
        Assert.Equal(OperationStatus.Failed, result.Status);
        Assert.Equal("disk full", result.Error);
        Assert.Throws<ArgumentException>(() => OperationResult.Failure(" "));
    }

    [Fact]
    public void NotImplemented_uses_a_stable_message_and_no_value()
    {
        var result = OperationResult<string>.NotImplemented("Hardware report");

        Assert.False(result.Succeeded);
        Assert.Equal(OperationStatus.NotImplemented, result.Status);
        Assert.Equal("Hardware report is not implemented.", result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Generic_success_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => OperationResult<string>.Success(null!));
    }
}

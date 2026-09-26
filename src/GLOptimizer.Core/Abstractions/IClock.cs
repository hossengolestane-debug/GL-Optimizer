namespace GLOptimizer.Core.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

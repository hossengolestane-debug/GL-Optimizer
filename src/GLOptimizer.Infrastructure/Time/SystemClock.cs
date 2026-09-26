using GLOptimizer.Core.Abstractions;

namespace GLOptimizer.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

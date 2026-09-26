using GLOptimizer.Core.Configuration;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

/// <summary>
/// Reads GameLoop configuration for install paths that were already verified. Implementations must not create or modify files.
/// </summary>
public interface IGameLoopConfigDiscovery
{
    Task<OperationResult<GameLoopConfigReport>> DiscoverAsync(
        IReadOnlyList<string> installPaths,
        CancellationToken cancellationToken = default);
}

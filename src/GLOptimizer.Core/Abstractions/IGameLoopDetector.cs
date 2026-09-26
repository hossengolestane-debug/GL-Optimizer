using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

/// <summary>
/// Read-only GameLoop discovery. Implementations must not write config files or invent versions.
/// </summary>
public interface IGameLoopDetector
{
    Task<OperationResult<GameLoopScan>> DetectAsync(CancellationToken cancellationToken = default);
}

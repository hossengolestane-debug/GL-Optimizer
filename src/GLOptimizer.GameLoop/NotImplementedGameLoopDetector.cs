using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.GameLoop;

/// <summary>
/// Phase 0 stub. Does not search for, read, or modify GameLoop files.
/// </summary>
public sealed class NotImplementedGameLoopDetector : IGameLoopDetector
{
    public OperationResult<GameLoopInstallation> Detect() =>
        OperationResult<GameLoopInstallation>.NotImplemented("GameLoop detection");
}

using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

/// <summary>
/// GameLoop discovery. Phase 0 must not read or modify GameLoop files.
/// </summary>
public interface IGameLoopDetector
{
    OperationResult<GameLoopInstallation> Detect();
}

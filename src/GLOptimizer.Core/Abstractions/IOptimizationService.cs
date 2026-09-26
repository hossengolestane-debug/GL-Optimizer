using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

/// <summary>
/// Future optimization actions. Phase 0 must not change priorities, registry values, or game files.
/// </summary>
public interface IOptimizationService
{
    OperationResult<IReadOnlyList<OptimizationAction>> ListActions();

    OperationResult Apply(string actionId);
}

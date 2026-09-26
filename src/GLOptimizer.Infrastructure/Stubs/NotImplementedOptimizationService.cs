using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Infrastructure.Stubs;

/// <summary>
/// Phase 0 stub. Does not change processes, registry values, or game files.
/// </summary>
public sealed class NotImplementedOptimizationService : IOptimizationService
{
    public OperationResult<IReadOnlyList<OptimizationAction>> ListActions() =>
        OperationResult<IReadOnlyList<OptimizationAction>>.NotImplemented("Optimization actions");

    public OperationResult Apply(string actionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        return OperationResult.NotImplemented("Optimization apply");
    }
}

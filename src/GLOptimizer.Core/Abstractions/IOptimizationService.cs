using GLOptimizer.Core.Backup;
using GLOptimizer.Core.Optimization;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

/// <summary>
/// Previews and applies GameLoop configuration changes. Selecting a profile does not write.
/// </summary>
public interface IOptimizationService
{
    Task<OperationResult<OptimizationAnalysis>> AnalyzeAsync(OptimizationProfile profile, CancellationToken cancellationToken = default);

    Task<OperationResult<OptimizationPreview>> PreviewAsync(OptimizationProfile profile, CancellationToken cancellationToken = default);

    Task<OperationResult<OptimizationReport>> ApplyAsync(OptimizationProfile profile, bool confirmed, CancellationToken cancellationToken = default);

    Task<OperationResult<RestoreReport>> UndoLastAsync(bool confirmed, CancellationToken cancellationToken = default);
}

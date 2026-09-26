using GLOptimizer.Core.Optimization;

namespace GLOptimizer.Core.Abstractions;

/// <summary>
/// Windows graphics preference, process priority, power mode, and background apps.
/// This phase may describe them. It must not change them.
/// </summary>
public interface IHostOptimizationProbe
{
    IReadOnlyList<OptimizationRecommendation> Detect();
}

using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Optimization;

namespace GLOptimizer.Infrastructure.Optimization;

public sealed class NotImplementedHostOptimizationProbe : IHostOptimizationProbe
{
    public IReadOnlyList<OptimizationRecommendation> Detect() =>
    [
        Item("Graphics preference", "Detect only. Changing the Windows graphics preference for GameLoop is not implemented."),
        Item("Process priority", "Detect only. Process priority is not changed."),
        Item("Power mode", "Detect only. The Windows power mode is not changed."),
        Item("Background apps", "Detect only. Background processes are not closed.")
    ];

    private static OptimizationRecommendation Item(string setting, string reason) => new()
    {
        Setting = setting,
        Reason = reason,
        Status = RecommendationStatus.NotImplemented,
        Risk = RiskLevel.Low
    };
}

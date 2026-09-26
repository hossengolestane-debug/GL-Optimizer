using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

/// <summary>
/// Frame timing. Implementations must not inject into a game process or fabricate FPS.
/// </summary>
public interface IFrameMetricsProvider
{
    OperationResult<FrameSample> TryGetLatest();
}

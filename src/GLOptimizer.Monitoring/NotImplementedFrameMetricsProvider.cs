using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Monitoring;

/// <summary>
/// No safe non-invasive frame provider is available. This does not inject, read game memory, or invent FPS.
/// </summary>
public sealed class NotImplementedFrameMetricsProvider : IFrameMetricsProvider
{
    public OperationResult<FrameSample> TryGetLatest() =>
        OperationResult<FrameSample>.NotImplemented("Frame metrics");
}

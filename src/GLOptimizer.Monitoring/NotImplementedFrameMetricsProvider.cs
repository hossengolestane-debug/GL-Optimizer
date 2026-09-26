using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Monitoring;

/// <summary>
/// Phase 0 stub. Does not attach to a process and does not invent frame timings.
/// </summary>
public sealed class NotImplementedFrameMetricsProvider : IFrameMetricsProvider
{
    public OperationResult<FrameSample> TryGetLatest() =>
        OperationResult<FrameSample>.NotImplemented("Frame metrics");
}

using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Monitoring;

/// <summary>
/// Phase 0 stub. Does not query WMI, performance counters, or any device, and does not invent readings.
/// </summary>
public sealed class NotImplementedHardwareService : IHardwareService
{
    public OperationResult<HardwareReport> TryGetReport() =>
        OperationResult<HardwareReport>.NotImplemented("Hardware report");
}

using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

/// <summary>
/// Hardware inventory. Missing fields stay null. Implementations must not invent CPU, GPU, or memory figures.
/// </summary>
public interface IHardwareService
{
    Task<OperationResult<HardwareReport>> GetReportAsync(CancellationToken cancellationToken = default);
}

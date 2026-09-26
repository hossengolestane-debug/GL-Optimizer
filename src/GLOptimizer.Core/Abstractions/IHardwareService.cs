using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

/// <summary>
/// Hardware inventory. Implementations must return <see cref="OperationStatus.NotImplemented"/>
/// until they can report values they actually queried. Never invent CPU, GPU, or memory figures.
/// </summary>
public interface IHardwareService
{
    OperationResult<HardwareReport> TryGetReport();
}

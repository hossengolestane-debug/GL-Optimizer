using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

/// <summary>
/// Benchmark mode is described here and is not implemented.
/// </summary>
public interface IBenchmarkService
{
    OperationResult Describe();
}

using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Infrastructure.Optimization;

public sealed class NotImplementedBenchmarkService : IBenchmarkService
{
    public OperationResult Describe() => OperationResult.NotImplemented("Benchmark mode is not implemented.");
}

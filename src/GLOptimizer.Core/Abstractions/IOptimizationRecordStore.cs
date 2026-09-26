using GLOptimizer.Core.Optimization;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

public interface IOptimizationRecordStore
{
    string? LastProblem => null;

    OperationResult Save(OptimizationUndoRecord record);

    OperationResult<OptimizationUndoRecord> Read();

    OperationResult Clear();
}

using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

/// <summary>
/// App Market checks. Phase 0 must not read or modify App Market files.
/// </summary>
public interface IAppMarketDiagnostics
{
    OperationResult<AppMarketStatus> Check();
}

using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.GameLoop;

/// <summary>
/// Phase 0 stub. Does not read or modify App Market files.
/// </summary>
public sealed class NotImplementedAppMarketDiagnostics : IAppMarketDiagnostics
{
    public OperationResult<AppMarketStatus> Check() =>
        OperationResult<AppMarketStatus>.NotImplemented("App Market diagnostics");
}

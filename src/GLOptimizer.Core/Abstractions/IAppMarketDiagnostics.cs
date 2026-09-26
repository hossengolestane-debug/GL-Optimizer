using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

/// <summary>
/// Reads App Market files under a verified GameLoop install. It does not delete, clear, or repair them.
/// </summary>
public interface IAppMarketDiagnostics
{
    Task<OperationResult<AppMarketReport>> ScanAsync(bool checkOfficialVersion, CancellationToken cancellationToken = default);
}

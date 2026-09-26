using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

public interface ICodMobileDiagnostics
{
    Task<OperationResult<CodMobileReport>> RunAsync(bool checkOfficialVersion, CancellationToken cancellationToken = default);
}

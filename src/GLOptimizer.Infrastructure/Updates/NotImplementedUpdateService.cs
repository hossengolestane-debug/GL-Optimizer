using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Infrastructure.Updates;

public sealed class NotImplementedUpdateService : IUpdateService
{
    public int NetworkRequests { get; private set; }

    public Task<OperationResult<UpdateOffer>> CheckAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(OperationResult<UpdateOffer>.NotImplemented("GL Optimizer update check"));
    }
}

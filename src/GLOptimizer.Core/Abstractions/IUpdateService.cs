using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

public sealed class UpdateOffer
{
    public string? Version { get; init; }

    public string? Sha256 { get; init; }

    public string? PackageUrl { get; init; }

    public bool RequestedNetwork { get; init; }
}

public interface IUpdateService
{
    Task<OperationResult<UpdateOffer>> CheckAsync(CancellationToken cancellationToken = default);
}

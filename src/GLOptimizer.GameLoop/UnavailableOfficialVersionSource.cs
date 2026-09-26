using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Models;

namespace GLOptimizer.GameLoop;

/// <summary>
/// Official version lookup is not wired to a GameLoop or Tencent endpoint.
/// No host is contacted.
/// </summary>
public sealed class UnavailableOfficialVersionSource : IOfficialVersionSource
{
    public const string NotImplementedDetail = "Official version source not implemented.";

    public Task<OfficialVersionResult> TryGetAsync(string? packageId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new OfficialVersionResult
        {
            Version = null,
            Host = null,
            RequestedNetwork = false,
            Detail = NotImplementedDetail
        });
    }
}

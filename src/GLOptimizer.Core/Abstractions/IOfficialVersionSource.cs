using GLOptimizer.Core.Models;

namespace GLOptimizer.Core.Abstractions;

/// <summary>
/// Official COD Mobile version. Implementations must not call the network unless the caller asked for a version check.
/// </summary>
public interface IOfficialVersionSource
{
    Task<OfficialVersionResult> TryGetAsync(string? packageId, CancellationToken cancellationToken = default);
}

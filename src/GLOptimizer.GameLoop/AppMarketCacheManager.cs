using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Models;

namespace GLOptimizer.GameLoop;

/// <summary>
/// Lists cache entries from an inventory. It does not delete or clear them.
/// </summary>
public sealed class AppMarketCacheManager
{
    public IReadOnlyList<MarketInventoryItem> ListCaches(IReadOnlyList<MarketInventoryItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var caches = new List<MarketInventoryItem>();
        foreach (var item in items)
        {
            if (item.Kind == MarketItemKind.Cache)
            {
                caches.Add(item);
            }
        }

        return caches;
    }
}

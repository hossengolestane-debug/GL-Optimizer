using GLOptimizer.Core.Models;

namespace GLOptimizer.Core.Detection;

/// <summary>
/// Install directories plus GameLoopData directories from a finished scan.
/// </summary>
public static class GameLoopLocations
{
    public static IReadOnlyList<string> InstallAndData(GameLoopScan scan)
    {
        ArgumentNullException.ThrowIfNull(scan);
        var roots = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var installation in scan.Installations)
        {
            Add(roots, seen, installation.InstallPath);
            Add(roots, seen, installation.DataPath);
        }

        foreach (var data in scan.DataRoots)
        {
            Add(roots, seen, data);
        }

        return roots;
    }

    private static void Add(List<string> roots, HashSet<string> seen, string? path)
    {
        var normalized = InstallPathRules.TryNormalize(path);
        if (normalized is not null && seen.Add(normalized))
        {
            roots.Add(normalized);
        }
    }
}

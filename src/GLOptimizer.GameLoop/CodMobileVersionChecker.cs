using System.Text;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;

namespace GLOptimizer.GameLoop;

public sealed class CodMobileVersionChecker
{
    public CatalogComparisonResult Check(string? installed, string? market, string? official) =>
        CatalogComparisonLogic.Evaluate(installed, market, official);

    public string? ReadInstalledVersion(string? packageDirectory)
    {
        if (string.IsNullOrWhiteSpace(packageDirectory))
        {
            return null;
        }

        var root = InstallPathRules.TryNormalize(packageDirectory);
        if (root is null || !Directory.Exists(root))
        {
            return null;
        }

        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in GameLoopLayout.VersionFileNames)
        {
            var path = Path.Combine(root, name);
            if (!File.Exists(path) || !InstallPathRules.IsUnderRoot(path, root) || IsReparse(path))
            {
                continue;
            }

            var version = ReadTextVersion(path);
            if (version is not null)
            {
                found.Add(version);
            }
        }

        return found.Count == 1 ? found.First() : null;
    }

    internal static string? ReadTextVersion(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var length = (int)Math.Min(stream.Length, 65536);
            var buffer = new byte[length];
            if (length > 0)
            {
                stream.ReadExactly(buffer);
            }

            return PackageVersion.FromContent(Encoding.UTF8.GetString(buffer));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static bool IsReparse(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return true;
        }
    }
}

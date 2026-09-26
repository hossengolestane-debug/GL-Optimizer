using GLOptimizer.Core.Detection;

namespace GLOptimizer.GameLoop;

public interface IGameLoopEnvironment
{
    IReadOnlyList<UninstallHint> ReadUninstallHints();

    UninstallRead ReadUninstall()
    {
        var hints = ReadUninstallHints();
        var attempts = new List<RegistryAttempt>();
        foreach (var hint in hints)
        {
            if (!GameLoopNames.IsProduct(hint.DisplayName))
            {
                continue;
            }

            attempts.Add(new RegistryAttempt
            {
                Location = string.IsNullOrWhiteSpace(hint.Source) ? "Uninstall\\" + hint.DisplayName : hint.Source,
                Found = true,
                Detail = hint.DisplayVersion
            });
        }

        return new UninstallRead(hints, attempts);
    }

    IReadOnlyList<ProductRegistration> ReadProductRegistrations() => [];

    IReadOnlyList<string> MarketDirectories() => [];

    IReadOnlyList<string> CandidateDirectories();

    IReadOnlyList<string> StartMenuTargets();

    IReadOnlyList<string> MobileDataRoots();

    ProcessQueryResult QueryProcesses();

    bool FileExists(string path);

    bool DirectoryExists(string path);

    DirectorySearchResult FindDirectoriesNamed(string root, IReadOnlyCollection<string> names, int maxDepth, int maxNodes, CancellationToken cancellationToken);

    string? ReadSmallText(string path, int maxBytes);

    string? TryReadFileVersion(string path);
}

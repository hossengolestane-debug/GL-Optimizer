namespace GLOptimizer.GameLoop;

public interface IGameLoopEnvironment
{
    IReadOnlyList<UninstallHint> ReadUninstallHints();

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

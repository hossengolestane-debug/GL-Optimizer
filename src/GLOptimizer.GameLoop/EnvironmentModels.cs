namespace GLOptimizer.GameLoop;

public sealed class UninstallHint
{
    public string? DisplayName { get; init; }

    public string? InstallLocation { get; init; }

    public string? DisplayIcon { get; init; }

    public string? DisplayVersion { get; init; }
}

public sealed class ProcessObservation
{
    public int ProcessId { get; init; }

    public string ProcessName { get; init; } = string.Empty;

    public string? ExecutablePath { get; init; }
}

public sealed class ProcessQueryResult
{
    public bool Available { get; init; }

    public bool HadUnreadableMatch { get; init; }

    public IReadOnlyList<ProcessObservation> Processes { get; init; } = [];
}

public sealed class DirectorySearchResult
{
    public IReadOnlyList<string> Matches { get; init; } = [];

    public bool Completed { get; init; }
}

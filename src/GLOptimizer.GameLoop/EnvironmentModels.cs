namespace GLOptimizer.GameLoop;

public sealed class UninstallHint
{
    public string? DisplayName { get; init; }

    public string? InstallLocation { get; init; }

    public string? DisplayIcon { get; init; }

    public string? UninstallString { get; init; }

    public string? DisplayVersion { get; init; }

    /// <summary>
    /// Registry key that produced this hint, including the view.
    /// </summary>
    public string? Source { get; init; }
}

public sealed class ProductRegistration
{
    public string Location { get; init; } = string.Empty;

    public bool Found { get; init; }

    public string? InstallPath { get; init; }

    public string? DataPath { get; init; }

    public string? Version { get; init; }

    public string? Detail { get; init; }
}

public sealed class RegistryAttempt
{
    public string Location { get; init; } = string.Empty;

    public bool Found { get; init; }

    public string? Detail { get; init; }
}

public sealed class UninstallRead
{
    public UninstallRead(IReadOnlyList<UninstallHint> hints, IReadOnlyList<RegistryAttempt> attempts)
    {
        Hints = hints;
        Attempts = attempts;
    }

    public IReadOnlyList<UninstallHint> Hints { get; }

    public IReadOnlyList<RegistryAttempt> Attempts { get; }
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

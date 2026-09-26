namespace GLOptimizer.Core.Configuration;

public enum ConfigFileKind
{
    EngineSettings = 0,
    UserSettings = 1,
    KeyMap = 2,
    Registry = 3
}

public enum ConfigPresence
{
    NotFound = 0,
    Present = 1,
    Unreadable = 2
}

public sealed class ConfigFileRecord
{
    public required string Path { get; init; }

    public ConfigFileKind Kind { get; init; }

    public ConfigPresence Presence { get; init; }

    public long? SizeBytes { get; init; }

    public DateTimeOffset? LastWriteTime { get; init; }

    public string? Detail { get; init; }
}

public sealed class InstallConfigReport
{
    public required string InstallPath { get; init; }

    public GameLoopSettings Settings { get; init; } = new();

    public IReadOnlyList<ConfigFileRecord> Files { get; init; } = [];
}

public sealed class GameLoopConfigReport
{
    public IReadOnlyList<InstallConfigReport> Installs { get; init; } = [];

    public GameLoopSettings? SharedSettings { get; init; }

    public IReadOnlyList<ConfigFileRecord> SharedFiles { get; init; } = [];

    public string? Notice { get; init; }
}

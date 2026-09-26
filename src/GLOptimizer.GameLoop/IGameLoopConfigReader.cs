namespace GLOptimizer.GameLoop;

public sealed class ConfigProbe
{
    public bool Exists { get; init; }

    public bool ReadFailed { get; init; }

    public bool TooLarge { get; init; }

    public bool ReparsePoint { get; init; }

    public long? SizeBytes { get; init; }

    public DateTimeOffset? LastWriteTime { get; init; }

    public string? Text { get; init; }

    public static ConfigProbe Missing() => new();

    public static ConfigProbe Failed() => new() { Exists = true, ReadFailed = true };
}

public sealed class RegistryProbe
{
    public bool Found { get; init; }

    public bool Failed { get; init; }

    public IReadOnlyDictionary<string, string> Values { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> UnmappedRendererKeys { get; init; } = [];
}

public interface IGameLoopConfigReader
{
    ConfigProbe ProbeFile(string path, bool readText);

    RegistryProbe ReadMobileGamePc();

    IReadOnlyList<string> KnownUserFiles();
}

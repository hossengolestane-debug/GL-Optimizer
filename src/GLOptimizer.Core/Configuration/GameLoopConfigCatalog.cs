namespace GLOptimizer.Core.Configuration;

public sealed class ConfigCandidate
{
    public ConfigCandidate(IReadOnlyList<string> segments, ConfigFileKind kind)
    {
        Segments = segments;
        Kind = kind;
    }

    public IReadOnlyList<string> Segments { get; }

    public ConfigFileKind Kind { get; }
}

/// <summary>
/// Fixed locations. Discovery does not walk the install or the user profile.
/// </summary>
public static class GameLoopConfigCatalog
{
    public const int MaxTextBytes = 64 * 1024;

    public const string RegistryPath = @"HKCU\Software\Tencent\MobileGamePC";

    public static IReadOnlyList<ConfigCandidate> InstallRelative { get; } =
    [
        new(["ui", "DefaultKeyMapping.xml"], ConfigFileKind.KeyMap),
        new(["UI", "DefaultKeyMapping.xml"], ConfigFileKind.KeyMap),
        new(["ui", "config.ini"], ConfigFileKind.EngineSettings),
        new(["UI", "config.ini"], ConfigFileKind.EngineSettings),
        new(["ui", "UserConfig.ini"], ConfigFileKind.EngineSettings),
        new(["UI", "UserConfig.ini"], ConfigFileKind.EngineSettings),
        new(["config.ini"], ConfigFileKind.EngineSettings),
        new(["app.ini"], ConfigFileKind.EngineSettings)
    ];

    public static IReadOnlyList<string> UserFileNames { get; } =
    [
        "config.ini",
        "UserConfig.ini",
        "ui_config.json",
        "config.json"
    ];

    public static IReadOnlyList<string> UserDirectoryNames { get; } =
    [
        Path.Combine("Tencent", "MobileGamePC"),
        Path.Combine("Tencent", "GameLoop")
    ];
}

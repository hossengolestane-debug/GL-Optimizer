namespace GLOptimizer.Core.Detection;

/// <summary>
/// Relative locations used while searching. None of these is assumed to be the only install path.
/// </summary>
public static class GameLoopLayout
{
    public static IReadOnlyList<string> RelativeRoots { get; } =
    [
        Path.Combine("Program Files", "TxGameAssistant"),
        Path.Combine("Program Files", "GameLoop"),
        Path.Combine("Program Files (x86)", "TxGameAssistant"),
        Path.Combine("Program Files (x86)", "GameLoop"),
        "TxGameAssistant",
        "GameLoop"
    ];

    public static IReadOnlyList<string[]> LauncherSegments { get; } =
    [
        ["GameLoop.exe"],
        ["AppMarket.exe"],
        ["TxGameAssistant.exe"],
        ["ui", "AndroidEmulator.exe"],
        ["UI", "AndroidEmulator.exe"],
        ["AndroidEmulatorEn.exe"],
        ["ui", "AndroidEmulatorEn.exe"]
    ];

    public static IReadOnlyList<string[]> EngineSegments { get; } =
    [
        ["aow_exe.exe"],
        ["Engine", "aow_exe.exe"],
        ["ui", "aow_exe.exe"],
        ["AOW", "aow_exe.exe"]
    ];

    public static IReadOnlyList<string[]> PackageParents { get; } =
    [
        ["ui", "Android", "data"],
        ["UI", "Android", "data"],
        ["Android", "data"],
        ["AOW", "data", "data"],
        ["data", "data"],
        ["Engine", "data", "data"]
    ];

    public static IReadOnlyList<string> VersionFileNames { get; } =
    [
        "version.txt",
        "version",
        "info.json",
        "config.ini",
        "app.ini",
        "manifest.json"
    ];

    public static string Combine(string root, IReadOnlyList<string> segments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        var parts = new string[segments.Count + 1];
        parts[0] = root;
        for (var i = 0; i < segments.Count; i++)
        {
            parts[i + 1] = segments[i];
        }

        return Path.Combine(parts);
    }
}

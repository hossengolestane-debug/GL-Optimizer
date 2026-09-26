namespace GLOptimizer.Core.Detection;

public static class GameLoopNames
{
    public static IReadOnlyList<string> ProcessNames { get; } =
    [
        "GameLoop",
        "AppMarket",
        "TxGameAssistant",
        "AndroidEmulator",
        "AndroidEmulatorEn",
        "aow_exe"
    ];

    public static bool IsProduct(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.Contains("GameLoop", StringComparison.OrdinalIgnoreCase)
            || name.Contains("TxGameAssistant", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsProcess(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        foreach (var candidate in ProcessNames)
        {
            if (name.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

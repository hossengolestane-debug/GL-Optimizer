namespace GLOptimizer.Core.Diagnostics;

/// <summary>
/// Startup used by the Windows installer workflow. It lays the main window out off-screen and
/// may run read-only page refreshes. It must not write GameLoop files, sample performance, or watch a launch journal.
/// </summary>
public static class SmokeTest
{
    public const string Flag = "--smoke-test";

    public const string Marker = "GL Optimizer smoke test ok";

    public static bool Active { get; private set; }

    public static bool IsRequested(IReadOnlyList<string>? args)
    {
        if (args is null)
        {
            return false;
        }

        foreach (var arg in args)
        {
            if (string.Equals(arg, Flag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static void MarkActive() => Active = true;
}

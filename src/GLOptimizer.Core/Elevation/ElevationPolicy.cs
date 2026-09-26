using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Elevation;

public static class ElevationPolicy
{
    public const string RunsAsInvoker =
        "GL Optimizer runs as the current user and does not request administrator rights at startup.";

    public static string Explain(string operation) =>
        operation + " needs elevation. " + RunsAsInvoker + " You can relaunch only this operation with elevation.";

    public static bool IsOperation(string? operation) =>
        operation is "launch-optimized" or "restore-priority";

    public static string? Argument(string? operation) =>
        IsOperation(operation) ? "--operation " + operation : null;

    public static string? Read(IReadOnlyList<string>? args)
    {
        if (args is null)
        {
            return null;
        }

        for (var index = 0; index < args.Count - 1; index++)
        {
            if (string.Equals(args[index], "--operation", StringComparison.Ordinal) && IsOperation(args[index + 1]))
            {
                return args[index + 1];
            }
        }

        return null;
    }
}

public interface IElevationRelaunch
{
    OperationResult Relaunch(string operation);
}

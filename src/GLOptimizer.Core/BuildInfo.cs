using System.Reflection;

namespace GLOptimizer.Core;

public static class BuildInfo
{
    public const string ProductName = "GL Optimizer";
    public const string PhaseName = "Phase 0";

    public static string Version
    {
        get
        {
            var informational = typeof(BuildInfo).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrWhiteSpace(informational))
            {
                return "0.1.0";
            }

            var plus = informational.IndexOf('+', StringComparison.Ordinal);
            return plus > 0 ? informational[..plus] : informational;
        }
    }

    public static bool IsDeveloperMode
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }
}

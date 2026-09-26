using GLOptimizer.Core.Models;

namespace GLOptimizer.Core.Diagnostics;

/// <summary>
/// Simulated GameLoop, game, and metric values exist only in Debug builds.
/// </summary>
public static class DeveloperSimulation
{
    public static bool IsAvailable => Available();

    public static GameLoopScan? Scan() => SimulatedScan();

    public static string? CodVersion() => SimulatedCod();

    public static string? MarketVersion() => SimulatedMarket();

    public static double? CpuPercent() => SimulatedCpu();

    private static bool Available()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }

    private static GameLoopScan? SimulatedScan()
    {
#if DEBUG
        return new GameLoopScan
        {
            Installations =
            [
                new GameLoopInstallation
                {
                    InstallPath = @"C:\Simulated\GameLoop",
                    LauncherPath = @"C:\Simulated\GameLoop\GameLoop.exe",
                    Version = "9.9.9"
                }
            ],
            PubgMobile = new MobileGamePresence
            {
                Status = GamePresenceStatus.Installed,
                PackageId = "com.tencent.ig",
                Version = "3.0.0",
                Detail = "Simulated PUBG Mobile. Debug builds only."
            },
            CodMobile = new MobileGamePresence
            {
                Status = GamePresenceStatus.Installed,
                PackageId = "com.activision.callofduty.shooter",
                Version = "1.0.50",
                Detail = "Simulated COD Mobile. Debug builds only."
            },
            Warnings = ["Simulated CPU sample: 12%. Debug builds only."]
        };
#else
        return null;
#endif
    }

    private static string? SimulatedCod()
    {
#if DEBUG
        return "1.0.50";
#else
        return null;
#endif
    }

    private static string? SimulatedMarket()
    {
#if DEBUG
        return "1.0.40";
#else
        return null;
#endif
    }

    private static double? SimulatedCpu()
    {
#if DEBUG
        return 12;
#else
        return null;
#endif
    }
}

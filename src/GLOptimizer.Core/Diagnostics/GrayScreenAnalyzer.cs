using System.Globalization;
using GLOptimizer.Core.Detection;

namespace GLOptimizer.Core.Diagnostics;

public enum FindingOutcome
{
    Pass = 0,
    Warning = 1,
    Failed = 2,
    Unknown = 3
}

public sealed class DiagnosticFinding
{
    public required string Title { get; init; }

    public required string Evidence { get; init; }

    public required string RecommendedAction { get; init; }

    public int Rank { get; init; }

    public FindingOutcome Outcome { get; init; }
}

/// <summary>
/// Facts collected without writing or killing a process. Null means the check did not produce a value.
/// </summary>
public sealed class GrayScreenEvidence
{
    public bool? GameLoopRunning { get; init; }

    public bool? EngineProcessRunning { get; init; }

    public bool? CodProcessRunning { get; init; }

    /// <summary>Null when window titles were not enumerated.</summary>
    public bool? CodWindowSeen { get; init; }

    public bool ProcessListDefinitive { get; init; }

    public bool WindowProbeSucceeded { get; init; }

    public bool EngineCpuSampled { get; init; }

    public double? EngineCpuPercent { get; init; }

    public bool SystemGpuSampled { get; init; }

    public double? SystemGpuPercent { get; init; }

    public CatalogComparison Comparison { get; init; }

    public string? ComparisonDetail { get; init; }

    public string? Renderer { get; init; }

    public bool? CacheStale { get; init; }

    public string? CacheDetail { get; init; }

    public bool LogFileFound { get; init; }

    public IReadOnlyList<string> LogErrorLines { get; init; } = [];
}

/// <summary>
/// Ranks gray-screen checks from evidence that was already collected. It does not assign a probability or a cause.
/// </summary>
public static class GrayScreenAnalyzer
{
    public static IReadOnlyList<DiagnosticFinding> Rank(GrayScreenEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var findings = new List<DiagnosticFinding>
        {
            Comparison(evidence),
            Logs(evidence),
            GameLoop(evidence),
            Engine(evidence),
            CodSurface(evidence),
            Cpu(evidence),
            Gpu(evidence),
            Cache(evidence),
            Renderer(evidence)
        };
        findings.Sort(static (left, right) => right.Rank.CompareTo(left.Rank));
        return findings;
    }

    private static DiagnosticFinding Comparison(GrayScreenEvidence evidence)
    {
        var detail = string.IsNullOrWhiteSpace(evidence.ComparisonDetail)
            ? "No comparison detail was recorded."
            : evidence.ComparisonDetail;
        return evidence.Comparison switch
        {
            CatalogComparison.RemoteCatalogIssue => Finding(
                "Server-side catalog",
                CatalogComparisonLogic.RemoteMessage,
                "This cannot safely be modified locally. Available in a later phase.",
                110,
                FindingOutcome.Failed),
            CatalogComparison.VersionMismatch => Finding(
                "COD Mobile version mismatch",
                detail,
                "Use Repair App Market on the App Market page.",
                100,
                FindingOutcome.Failed),
            CatalogComparison.LocalMarketOutdated => Finding(
                "Local market is older than the official version",
                detail,
                "Use Repair App Market on the App Market page.",
                90,
                FindingOutcome.Warning),
            CatalogComparison.Match => Finding(
                "Installed and market versions",
                detail,
                "No version action.",
                5,
                FindingOutcome.Pass),
            _ => Finding(
                "Version comparison",
                detail,
                "Check Version can request the official version. Repair is available in a later phase.",
                10,
                FindingOutcome.Unknown)
        };
    }

    private static DiagnosticFinding Logs(GrayScreenEvidence evidence)
    {
        if (evidence.LogErrorLines.Count > 0)
        {
            return Finding(
                "Engine log lines",
                string.Join(Environment.NewLine, evidence.LogErrorLines),
                "These lines were read locally and are not uploaded. Repair is available in a later phase.",
                80,
                FindingOutcome.Warning);
        }

        if (evidence.LogFileFound)
        {
            return Finding(
                "Engine log lines",
                "A log file under the verified install was read. No line contained error, fail, or exception.",
                "No log action.",
                8,
                FindingOutcome.Pass);
        }

        return Finding(
            "Engine log lines",
            "No engine log file was found under the verified install.",
            "No log action.",
            9,
            FindingOutcome.Unknown);
    }

    private static DiagnosticFinding GameLoop(GrayScreenEvidence evidence) => evidence.GameLoopRunning switch
    {
        true => Finding(
            "GameLoop process",
            "A GameLoop or TxGameAssistant process was found with an executable path inside the verified install.",
            "No action.",
            4,
            FindingOutcome.Pass),
        false => Finding(
            "GameLoop process",
            "The process list did not include GameLoop or TxGameAssistant under the verified install.",
            "Open in GameLoop is not implemented.",
            70,
            FindingOutcome.Warning),
        _ => Finding(
            "GameLoop process",
            "The process list did not show whether GameLoop is running.",
            "Run Diagnostics again.",
            12,
            FindingOutcome.Unknown)
    };

    private static DiagnosticFinding Engine(GrayScreenEvidence evidence)
    {
        if (evidence.EngineProcessRunning == true)
        {
            return Finding(
                "Android engine process",
                "An engine process (aow_exe or AndroidEmulator) was found under the verified install.",
                "No action.",
                3,
                FindingOutcome.Pass);
        }

        if (evidence.EngineProcessRunning == false && evidence.GameLoopRunning == true)
        {
            return Finding(
                "Android engine process",
                "GameLoop was running and the Android engine process was not found under the verified install.",
                "Restart Engine is not implemented.",
                60,
                FindingOutcome.Warning);
        }

        if (evidence.EngineProcessRunning == false)
        {
            return Finding(
                "Android engine process",
                "The Android engine process was not found under the verified install.",
                "Restart Engine is not implemented.",
                20,
                FindingOutcome.Unknown);
        }

        return Finding(
            "Android engine process",
            "The process list did not show whether the Android engine is running.",
            "Run Diagnostics again.",
            11,
            FindingOutcome.Unknown);
    }

    private static DiagnosticFinding CodSurface(GrayScreenEvidence evidence)
    {
        if (evidence.EngineProcessRunning != true)
        {
            return Finding(
                "COD Mobile process or window",
                "This was not treated as absent because the Android engine process was not confirmed running.",
                "Run Diagnostics again after the engine is running. Open in GameLoop is not implemented.",
                13,
                FindingOutcome.Unknown);
        }

        if (evidence.CodProcessRunning == true || evidence.CodWindowSeen == true)
        {
            var seen = evidence.CodProcessRunning == true
                ? "A COD Mobile process was found under the verified install."
                : "A window title matched COD Mobile and its process path is inside the verified install.";
            return Finding("COD Mobile process or window", seen, "No action.", 2, FindingOutcome.Pass);
        }

        var canCallAbsent = evidence.ProcessListDefinitive
            && evidence.CodProcessRunning == false
            && (!evidence.WindowProbeSucceeded || evidence.CodWindowSeen == false);
        if (!canCallAbsent)
        {
            return Finding(
                "COD Mobile process or window",
                "The process list or window titles were not complete enough to say whether COD Mobile is open.",
                "Run Diagnostics again.",
                13,
                FindingOutcome.Unknown);
        }

        var windowText = evidence.WindowProbeSucceeded
            ? "No matching window title was found for a process inside the install."
            : "Window titles were not enumerated, so this does not mean that no window exists.";
        return Finding(
            "COD Mobile process or window",
            "No COD Mobile process was found under the verified install. " + windowText,
            "Open in GameLoop is not implemented.",
            50,
            FindingOutcome.Warning);
    }

    private static DiagnosticFinding Cpu(GrayScreenEvidence evidence)
    {
        if (evidence.EngineProcessRunning == true && evidence.EngineCpuSampled && evidence.EngineCpuPercent is double cpu)
        {
            if (cpu == 0)
            {
                return Finding(
                    "Engine CPU sample",
                    "GameLoop process CPU was 0 across the sample window. This is an indicator, not a cause. System GPU is not attributed to the engine.",
                    "Restart Engine is not implemented.",
                    40,
                    FindingOutcome.Warning);
            }

            return Finding(
                "Engine CPU sample",
                "GameLoop process CPU was " + cpu.ToString("0.##", CultureInfo.InvariantCulture) + " during the sample window. This does not establish a cause.",
                "No action.",
                6,
                FindingOutcome.Pass);
        }

        return Finding(
            "Engine CPU sample",
            "A numeric engine CPU sample was not available. No activity level is assumed.",
            "Run Diagnostics again.",
            14,
            FindingOutcome.Unknown);
    }

    private static DiagnosticFinding Gpu(GrayScreenEvidence evidence)
    {
        if (evidence.SystemGpuSampled && evidence.SystemGpuPercent is double gpu)
        {
            return Finding(
                "System GPU sample",
                "System GPU was " + gpu.ToString("0.##", CultureInfo.InvariantCulture) + ". This sample is not attributed to the Android engine.",
                "No action.",
                2,
                FindingOutcome.Pass);
        }

        return Finding(
            "System GPU sample",
            "System GPU was not reported. It is not attributed to the Android engine.",
            "No action.",
            15,
            FindingOutcome.Unknown);
    }

    private static DiagnosticFinding Cache(GrayScreenEvidence evidence)
    {
        if (evidence.CacheStale == true)
        {
            var detail = string.IsNullOrWhiteSpace(evidence.CacheDetail)
                ? "A cache last-write time is earlier than the package last-write time."
                : evidence.CacheDetail;
            return Finding(
                "Local cache timestamp",
                detail + " This is an indicator, not a cause.",
                "Repair is available in Phase 8.",
                30,
                FindingOutcome.Warning);
        }

        if (evidence.CacheStale == false)
        {
            return Finding(
                "Local cache timestamp",
                string.IsNullOrWhiteSpace(evidence.CacheDetail)
                    ? "The cache last-write time is not earlier than the package."
                    : evidence.CacheDetail,
                "No cache action.",
                7,
                FindingOutcome.Pass);
        }

        return Finding(
            "Local cache timestamp",
            evidence.CacheDetail ?? "Cache and package last-write times were not both available.",
            "No cache action.",
            16,
            FindingOutcome.Unknown);
    }

    private static DiagnosticFinding Renderer(GrayScreenEvidence evidence)
    {
        if (!string.IsNullOrWhiteSpace(evidence.Renderer))
        {
            return Finding(
                "Renderer setting",
                "Renderer is " + evidence.Renderer + ". It was read from Phase 3 configuration.",
                "No change is made.",
                1,
                FindingOutcome.Pass);
        }

        return Finding(
            "Renderer setting",
            "Phase 3 configuration did not report a renderer.",
            "No change is made.",
            17,
            FindingOutcome.Unknown);
    }

    private static DiagnosticFinding Finding(string title, string evidence, string action, int rank, FindingOutcome outcome) => new()
    {
        Title = title,
        Evidence = evidence,
        RecommendedAction = action,
        Rank = rank,
        Outcome = outcome
    };
}

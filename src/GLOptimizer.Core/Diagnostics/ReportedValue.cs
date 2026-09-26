using System.Globalization;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Diagnostics;

/// <summary>
/// Turns a real <see cref="OperationResult{T}"/> into text. Missing or rejected numbers stay unavailable.
/// </summary>
public static class ReportedValue
{
    public const string Unavailable = "—";

    public static string Detail(bool succeeded, string? error) =>
        succeeded ? "Reported by the local service." : error ?? "No data.";

    public static string HardwareCpu(OperationResult<HardwareReport> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Succeeded && !string.IsNullOrWhiteSpace(result.Value?.CpuName)
            ? result.Value!.CpuName!
            : Unavailable;
    }

    public static string FramesPerSecond(OperationResult<FrameSample> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Succeeded || result.Value?.FramesPerSecond is not double fps)
        {
            return Unavailable;
        }

        if (double.IsNaN(fps) || double.IsInfinity(fps) || fps < 0)
        {
            return Unavailable;
        }

        return fps.ToString("0.#", CultureInfo.InvariantCulture);
    }

    public static string GameLoopVersion(OperationResult<GameLoopInstallation> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Succeeded && !string.IsNullOrWhiteSpace(result.Value?.Version)
            ? result.Value!.Version!
            : Unavailable;
    }

    public static string BackupCount(OperationResult<IReadOnlyList<BackupRecord>> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.Succeeded || result.Value is null)
        {
            return Unavailable;
        }

        return result.Value.Count.ToString(CultureInfo.InvariantCulture);
    }
}

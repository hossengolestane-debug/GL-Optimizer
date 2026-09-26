namespace GLOptimizer.Core.Configuration;

/// <summary>
/// Engine settings read from a GameLoop config. Null means the value was missing, conflicting, or not trusted.
/// </summary>
public sealed class GameLoopSettings
{
    public string? Renderer { get; init; }

    public string? Resolution { get; init; }

    public string? Dpi { get; init; }

    public string? MemoryAllocation { get; init; }

    public string? CpuAllocation { get; init; }

    public string? VSync { get; init; }

    public string? AntiAliasing { get; init; }

    public string? FpsTarget { get; init; }
}

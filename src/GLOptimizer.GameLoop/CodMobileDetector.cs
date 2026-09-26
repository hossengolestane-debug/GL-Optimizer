using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Models;

namespace GLOptimizer.GameLoop;

public sealed class CodMobileDetection
{
    public MobileGamePresence Presence { get; init; } = new();

    public IReadOnlyList<string> InstallPaths { get; init; } = [];

    public bool Succeeded { get; init; }

    public string? Error { get; init; }
}

public sealed class CodMobileDetector
{
    private readonly IGameLoopDetector _detector;

    public CodMobileDetector(IGameLoopDetector detector)
    {
        ArgumentNullException.ThrowIfNull(detector);
        _detector = detector;
    }

    public async Task<CodMobileDetection> DetectAsync(CancellationToken cancellationToken = default)
    {
        var scan = await _detector.DetectAsync(cancellationToken).ConfigureAwait(false);
        if (!scan.Succeeded || scan.Value is null)
        {
            return new CodMobileDetection
            {
                Succeeded = false,
                Error = scan.Error ?? "COD Mobile could not be scanned.",
                Presence = new MobileGamePresence
                {
                    Status = GamePresenceStatus.Unknown,
                    Detail = scan.Error ?? "COD Mobile could not be scanned."
                }
            };
        }

        var paths = new List<string>();
        foreach (var installation in scan.Value.Installations)
        {
            if (!string.IsNullOrWhiteSpace(installation.InstallPath))
            {
                paths.Add(installation.InstallPath);
            }
        }

        return new CodMobileDetection
        {
            Succeeded = true,
            Presence = scan.Value.CodMobile,
            InstallPaths = paths
        };
    }
}

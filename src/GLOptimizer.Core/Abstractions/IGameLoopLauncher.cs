using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

/// <summary>
/// Starts a verified GameLoop launcher. Close and restart stay unimplemented until a confirmed, non-destructive design exists.
/// </summary>
public interface IGameLoopLauncher
{
    Task<OperationResult> StartAsync(GameLoopInstallation installation, CancellationToken cancellationToken = default);

    Task<OperationResult> RestartAsync(GameLoopInstallation installation, CancellationToken cancellationToken = default);

    Task<OperationResult> CloseAsync(GameLoopInstallation installation, CancellationToken cancellationToken = default);
}

using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

/// <summary>
/// Starts a verified GameLoop launcher. Close and restart stop only processes whose executable path is inside that install.
/// Force stop requires a second confirmation from the caller.
/// </summary>
public interface IGameLoopLauncher
{
    Task<OperationResult> StartAsync(GameLoopInstallation installation, CancellationToken cancellationToken = default);

    Task<OperationResult> RestartAsync(
        GameLoopInstallation installation,
        bool forceConfirmed = false,
        CancellationToken cancellationToken = default);

    Task<OperationResult> CloseAsync(
        GameLoopInstallation installation,
        bool forceConfirmed = false,
        CancellationToken cancellationToken = default);
}

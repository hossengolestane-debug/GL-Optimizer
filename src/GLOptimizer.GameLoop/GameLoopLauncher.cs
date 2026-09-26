using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.GameLoop;

public sealed class GameLoopLauncher : IGameLoopLauncher
{
    private readonly IProcessStarter _starter;

    public GameLoopLauncher(IProcessStarter starter)
    {
        ArgumentNullException.ThrowIfNull(starter);
        _starter = starter;
    }

    public Task<OperationResult> StartAsync(GameLoopInstallation installation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installation);
        cancellationToken.ThrowIfCancellationRequested();
        if (!InstallPathRules.IsUnderRoot(installation.LauncherPath, installation.InstallPath))
        {
            return Task.FromResult(OperationResult.Failure("The launcher is not inside the verified GameLoop install."));
        }

        var launcher = InstallPathRules.TryNormalize(installation.LauncherPath);
        if (launcher is null || !File.Exists(launcher))
        {
            return Task.FromResult(OperationResult.Failure("The verified launcher file was not found."));
        }

        try
        {
            _starter.Start(launcher);
            return Task.FromResult(OperationResult.Success());
        }
        catch (Exception)
        {
            return Task.FromResult(OperationResult.Failure("GameLoop could not be started."));
        }
    }

    public Task<OperationResult> RestartAsync(GameLoopInstallation installation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installation);
        return Task.FromResult(OperationResult.NotImplemented("Restart GameLoop"));
    }

    public Task<OperationResult> CloseAsync(GameLoopInstallation installation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installation);
        return Task.FromResult(OperationResult.NotImplemented("Close GameLoop"));
    }
}

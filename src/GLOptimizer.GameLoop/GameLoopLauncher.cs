using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Repair;
using GLOptimizer.Core.Results;

namespace GLOptimizer.GameLoop;

public sealed class GameLoopLauncher : IGameLoopLauncher
{
    private readonly IProcessStarter _starter;
    private readonly GameLoopSessionStopper _stopper;

    public GameLoopLauncher(IProcessStarter starter)
        : this(starter, new IdleProcessControl(), new IdleDelay(), new IdleClock())
    {
    }

    public GameLoopLauncher(IProcessStarter starter, IProcessControl processes, ISampleDelay delay, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(starter);
        ArgumentNullException.ThrowIfNull(processes);
        ArgumentNullException.ThrowIfNull(delay);
        ArgumentNullException.ThrowIfNull(clock);
        _starter = starter;
        _stopper = new GameLoopSessionStopper(processes, delay, clock);
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

    public Task<OperationResult> RestartAsync(
        GameLoopInstallation installation,
        bool forceConfirmed = false,
        CancellationToken cancellationToken = default)
    {
        return RestartCoreAsync(installation, forceConfirmed, cancellationToken);
    }

    public Task<OperationResult> CloseAsync(
        GameLoopInstallation installation,
        bool forceConfirmed = false,
        CancellationToken cancellationToken = default)
    {
        return CloseCoreAsync(installation, forceConfirmed, cancellationToken);
    }

    private async Task<OperationResult> RestartCoreAsync(GameLoopInstallation installation, bool forceConfirmed, CancellationToken cancellationToken)
    {
        var closed = await CloseCoreAsync(installation, forceConfirmed, cancellationToken).ConfigureAwait(false);
        if (!closed.Succeeded)
        {
            return closed;
        }

        return await StartAsync(installation, cancellationToken).ConfigureAwait(false);
    }

    private async Task<OperationResult> CloseCoreAsync(GameLoopInstallation installation, bool forceConfirmed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(installation);
        var root = InstallPathRules.TryNormalize(installation.InstallPath);
        if (root is null)
        {
            return OperationResult.Failure("The GameLoop install path is not verified.");
        }

        var stopped = await _stopper.StopAsync([root], forceConfirmed, cancellationToken).ConfigureAwait(false);
        if (stopped.NeedsForceConfirmation)
        {
            return OperationResult.Failure(ProcessStopMessages.ForceRequired);
        }

        return stopped.Stopped
            ? OperationResult.Success()
            : OperationResult.Failure(stopped.Message);
    }

    private sealed class IdleProcessControl : IProcessControl
    {
        public IReadOnlyList<ControlledProcess> List() => [];

        public bool TryCloseMainWindow(int processId) => false;

        public bool TryTerminate(int processId) => false;
    }

    private sealed class IdleDelay : ISampleDelay
    {
        public Task WaitAsync(TimeSpan interval, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class IdleClock : IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}

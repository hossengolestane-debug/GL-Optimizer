using GLOptimizer.Core.Launch;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

public interface ILaunchOptimized
{
    LaunchRecovery Inspect();

    Task<OperationResult> ApplyAsync(bool confirmed, CancellationToken cancellationToken = default);

    Task<OperationResult> RecoverAsync(bool restore, CancellationToken cancellationToken = default);

    OperationResult PowerPlan();

    OperationResult GraphicsPreference();
}

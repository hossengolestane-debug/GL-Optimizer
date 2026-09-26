using GLOptimizer.Core.Repair;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

public sealed class AppMarketRepairPlan
{
    public required string Text { get; init; }

    public bool CanRepair { get; init; }

    public bool NeedsReview { get; init; }

    public string? ReviewReason { get; init; }

    public IReadOnlyList<string> ClearPaths { get; init; } = [];

    public IReadOnlyList<string> BackupPaths { get; init; } = [];

    public IReadOnlyList<string> StopPaths { get; init; } = [];
}

public sealed class AppMarketRepairResult
{
    public bool Completed { get; init; }

    public bool NeedsForceConfirmation { get; init; }

    public bool AwaitingRefresh { get; init; }

    public required string Message { get; init; }

    public RepairVerdictKind Verdict { get; init; }

    public string? BackupId { get; init; }

    public string? Toast { get; init; }
}

public interface IAppMarketRepair
{
    Task<OperationResult<AppMarketRepairPlan>> DryRunAsync(CancellationToken cancellationToken = default);

    Task<OperationResult<AppMarketRepairResult>> RepairAsync(
        bool confirmed,
        bool forceConfirmed,
        CancellationToken cancellationToken = default);

    Task<OperationResult<AppMarketRepairResult>> RecheckAsync(CancellationToken cancellationToken = default);

    Task<OperationResult<AppMarketRepairResult>> RollbackInterruptedAsync(bool confirmed, CancellationToken cancellationToken = default);
}

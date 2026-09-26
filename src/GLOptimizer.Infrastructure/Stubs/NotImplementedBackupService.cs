using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Infrastructure.Stubs;

/// <summary>
/// Phase 0 stub. Does not copy, delete, or restore any files.
/// </summary>
public sealed class NotImplementedBackupService : IBackupService
{
    public OperationResult<IReadOnlyList<BackupRecord>> List() =>
        OperationResult<IReadOnlyList<BackupRecord>>.NotImplemented("Backup list");

    public OperationResult Create(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return OperationResult.NotImplemented("Backup create");
    }

    public OperationResult Restore(string backupId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupId);
        return OperationResult.NotImplemented("Backup restore");
    }
}

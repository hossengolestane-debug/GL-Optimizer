using GLOptimizer.Core.Backup;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

public interface IBackupService
{
    OperationResult<IReadOnlyList<BackupRecord>> List();

    OperationResult<BackupDetails> Get(string backupId);

    Task<OperationResult<BackupDetails>> CreateAsync(string reason, CancellationToken cancellationToken = default);

    Task<OperationResult<RestorePreview>> PreviewRestoreAsync(string backupId, CancellationToken cancellationToken = default);

    Task<OperationResult<RestoreReport>> RestoreAsync(string backupId, bool confirmed, CancellationToken cancellationToken = default);

    Task<OperationResult> DeleteAsync(string backupId, bool confirmed, CancellationToken cancellationToken = default);
}

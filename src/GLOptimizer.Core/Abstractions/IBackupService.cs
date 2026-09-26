using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

public interface IBackupService
{
    OperationResult<IReadOnlyList<BackupRecord>> List();

    OperationResult Create(string label);

    OperationResult Restore(string backupId);
}

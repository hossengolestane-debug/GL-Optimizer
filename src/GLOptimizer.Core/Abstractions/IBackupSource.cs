using GLOptimizer.Core.Backup;

namespace GLOptimizer.Core.Abstractions;

/// <summary>
/// Supplies the files and registry text a backup may copy. Implementations must not write GameLoop files.
/// </summary>
public interface IBackupSource
{
    Task<BackupSourceSnapshot> CaptureAsync(CancellationToken cancellationToken = default);
}

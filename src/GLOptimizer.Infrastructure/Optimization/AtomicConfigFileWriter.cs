using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Backup;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Infrastructure.Optimization;

public sealed class AtomicConfigFileWriter : IConfigFileWriter
{
    public OperationResult Write(string path, byte[] contents)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(contents);
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return OperationResult.Failure("The target folder does not exist.");
        }

        if (BackupPathRules.IsReparse(directory) || BackupPathRules.IsReparse(path))
        {
            return OperationResult.Failure("The target is a link and was not replaced.");
        }

        var temp = Path.Combine(directory, ".glopt-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllBytes(temp, contents);
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temp, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Move(temp, path, overwrite: true);
                }
            }
            else
            {
                File.Move(temp, path);
            }

            return OperationResult.Success();
        }
        catch (UnauthorizedAccessException)
        {
            return OperationResult.Failure("Access was denied. Run GL Optimizer as administrator for this operation only. The rest of the app does not need to be elevated.");
        }
        catch (Exception ex)
        {
            var message = ex.Message.Trim();
            return OperationResult.Failure(message.Length > 240 ? message[..240] : message);
        }
        finally
        {
            if (File.Exists(temp))
            {
                try
                {
                    File.Delete(temp);
                }
                catch (Exception)
                {
                    // The temp file is best-effort.
                }
            }
        }
    }
}

using System.Diagnostics;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Results;

namespace GLOptimizer.App.Services;

public sealed class ExplorerFolderOpener : IFolderOpener
{
    public OperationResult Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return OperationResult.Failure("The folder is not available.");
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return OperationResult.Success();
        }
        catch (Exception)
        {
            return OperationResult.Failure("GL Optimizer could not open that folder.");
        }
    }
}

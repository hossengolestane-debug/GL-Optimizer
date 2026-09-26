using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

public interface IFolderOpener
{
    OperationResult Open(string path);
}

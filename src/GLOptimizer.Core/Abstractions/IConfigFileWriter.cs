using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

public interface IConfigFileWriter
{
    OperationResult Write(string path, byte[] contents);
}

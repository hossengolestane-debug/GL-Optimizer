using GLOptimizer.Core.Results;
using GLOptimizer.Core.Settings;

namespace GLOptimizer.Core.Abstractions;

public interface ISettingsStore
{
    AppSettings Current { get; }

    OperationResult Load();

    OperationResult Save(AppSettings settings);
}

using GLOptimizer.Core.Repair;

namespace GLOptimizer.Core.Abstractions;

public interface IProcessControl
{
    IReadOnlyList<ControlledProcess> List();

    bool TryCloseMainWindow(int processId);

    bool TryTerminate(int processId);
}

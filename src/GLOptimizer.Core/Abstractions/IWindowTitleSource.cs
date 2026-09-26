using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

public sealed class WindowTitle
{
    public int ProcessId { get; init; }

    public string Title { get; init; } = string.Empty;
}

/// <summary>
/// Window titles for processes the caller can already see. A host that cannot enumerate windows returns NotImplemented.
/// </summary>
public interface IWindowTitleSource
{
    OperationResult<IReadOnlyList<WindowTitle>> List();
}

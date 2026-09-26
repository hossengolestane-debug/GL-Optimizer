using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Diagnostics;

public static class StatusMapping
{
    public static StatusKind From(OperationStatus status) => status switch
    {
        OperationStatus.Success => StatusKind.Ready,
        OperationStatus.NotImplemented => StatusKind.Unavailable,
        OperationStatus.Failed => StatusKind.Attention,
        _ => StatusKind.Neutral
    };

    public static string Badge(OperationStatus status) => status switch
    {
        OperationStatus.Success => "Ready",
        OperationStatus.NotImplemented => "Not implemented",
        OperationStatus.Failed => "Needs attention",
        _ => "Unknown"
    };
}

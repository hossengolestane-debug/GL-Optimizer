namespace GLOptimizer.Core.Logging;

public sealed record LogEntry(
    DateTimeOffset Timestamp,
    LogSeverity Severity,
    string Category,
    string Message,
    string? Exception);

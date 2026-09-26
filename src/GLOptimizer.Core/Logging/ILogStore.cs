using GLOptimizer.Core.Settings;

namespace GLOptimizer.Core.Logging;

public interface ILogStore
{
    string LogDirectory { get; }

    string ActiveLogFilePath { get; }

    LogSeverity MinimumLevel { get; }

    string? LastError { get; }

    void ApplyPolicy(AppSettings settings);

    void Write(LogSeverity severity, string category, string message, Exception? exception = null);

    IReadOnlyList<LogEntry> GetRecent(int count = 200);

    IReadOnlyList<LogEntry> ReadActiveLog(int maxLines = 500);

    void Flush();
}

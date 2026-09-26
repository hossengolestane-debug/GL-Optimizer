using GLOptimizer.Core.Logging;
using Microsoft.Extensions.Logging;

namespace GLOptimizer.Infrastructure.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly ILogStore _store;

    public FileLoggerProvider(ILogStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, _store);

    public void Dispose()
    {
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _category;
        private readonly ILogStore _store;

        public FileLogger(string category, ILogStore store)
        {
            _category = string.IsNullOrWhiteSpace(category) ? "App" : category;
            _store = store;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel != LogLevel.None && Map(logLevel) >= _store.MinimumLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _store.Write(Map(logLevel), _category, formatter(state, exception), exception);
        }

        private static LogSeverity Map(LogLevel level) => level switch
        {
            LogLevel.Trace => LogSeverity.Trace,
            LogLevel.Debug => LogSeverity.Debug,
            LogLevel.Information => LogSeverity.Information,
            LogLevel.Warning => LogSeverity.Warning,
            LogLevel.Error => LogSeverity.Error,
            LogLevel.Critical => LogSeverity.Critical,
            _ => LogSeverity.Information
        };
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}

using System.Text;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Logging;
using GLOptimizer.Core.Settings;

namespace GLOptimizer.Infrastructure.Logging;

public sealed class FileLogStore : ILogStore, IDisposable
{
    private const int RecentCapacity = 500;
    private readonly IClock _clock;
    private readonly object _gate = new();
    private readonly Queue<LogEntry> _recent = new();
    private StreamWriter? _writer;
    private int _minimumLevel = (int)LogSeverity.Information;
    private long _maxBytes = AppSettingsRules.DefaultMaxLogFileBytes;
    private int _retentionDays = AppSettingsRules.DefaultRetentionDays;
    private bool _disposed;

    public FileLogStore(IClock clock, AppDataLocations locations)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(locations);
        _clock = clock;
        LogDirectory = locations.LogsDirectory;
        ActiveLogFilePath = locations.ActiveLogFile;
    }

    public string LogDirectory { get; }

    public string ActiveLogFilePath { get; }

    public LogSeverity MinimumLevel => (LogSeverity)Volatile.Read(ref _minimumLevel);

    public string? LastError { get; private set; }

    public void ApplyPolicy(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            Volatile.Write(ref _minimumLevel, (int)settings.MinimumLogLevel);
            _maxBytes = AppSettingsRules.ClampMaxLogFileBytes(settings.MaxLogFileBytes);
            _retentionDays = AppSettingsRules.ClampRetentionDays(settings.LogRetentionDays);
            PurgeExpired();
        }
    }

    public void Write(LogSeverity severity, string category, string message, Exception? exception = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentNullException.ThrowIfNull(message);
        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown log severity.");
        }

        ObjectDisposedException.ThrowIf(_disposed, this);
        if (severity < MinimumLevel)
        {
            return;
        }

        var entry = new LogEntry(
            _clock.UtcNow,
            severity,
            category.Trim(),
            message,
            exception?.ToString());

        lock (_gate)
        {
            _recent.Enqueue(entry);
            while (_recent.Count > RecentCapacity)
            {
                _recent.Dequeue();
            }

            var line = LogLine.Format(entry) + Environment.NewLine;
            var byteCount = Encoding.UTF8.GetByteCount(line);
            try
            {
                Directory.CreateDirectory(LogDirectory);
                RotateIfNeeded(byteCount);
                Writer.Write(line);
                Writer.Flush();
                LastError = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LastError = UserFacingError.From(ex);
            }
        }
    }

    public IReadOnlyList<LogEntry> GetRecent(int count = 200)
    {
        if (count <= 0)
        {
            return [];
        }

        lock (_gate)
        {
            return _recent.TakeLast(count).ToList();
        }
    }

    public IReadOnlyList<LogEntry> ReadActiveLog(int maxLines = 500)
    {
        if (maxLines <= 0)
        {
            return [];
        }

        lock (_gate)
        {
            try
            {
                _writer?.Flush();
                if (!File.Exists(ActiveLogFilePath))
                {
                    LastError = null;
                    return [];
                }

                var lines = File.ReadAllLines(ActiveLogFilePath);
                LastError = null;
                return lines
                    .TakeLast(maxLines)
                    .Select(line => LogLine.TryParse(line, out var entry) ? entry : null)
                    .OfType<LogEntry>()
                    .ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LastError = UserFacingError.From(ex);
                return [];
            }
        }
    }

    public void Flush()
    {
        lock (_gate)
        {
            _writer?.Flush();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _writer?.Dispose();
            _writer = null;
        }
    }

    private StreamWriter Writer => _writer ??= new StreamWriter(
        new FileStream(ActiveLogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
    {
        AutoFlush = false
    };

    private void RotateIfNeeded(int incomingBytes)
    {
        if (!File.Exists(ActiveLogFilePath))
        {
            return;
        }

        var length = new FileInfo(ActiveLogFilePath).Length;
        if (length == 0 || length + incomingBytes <= _maxBytes)
        {
            return;
        }

        _writer?.Dispose();
        _writer = null;

        for (var suffix = 0; suffix < 1000; suffix++)
        {
            var target = Path.Combine(LogDirectory, LogArchiveNames.Create(_clock.UtcNow, suffix));
            if (File.Exists(target))
            {
                continue;
            }

            File.Move(ActiveLogFilePath, target);
            return;
        }
    }

    private void PurgeExpired()
    {
        if (!Directory.Exists(LogDirectory))
        {
            return;
        }

        var now = _clock.UtcNow;
        foreach (var file in Directory.EnumerateFiles(LogDirectory, "gloptimizer-*.log").ToArray())
        {
            if (LogArchiveNames.IsExpired(Path.GetFileName(file), now, _retentionDays))
            {
                File.Delete(file);
            }
        }
    }
}

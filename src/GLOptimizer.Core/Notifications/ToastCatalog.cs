namespace GLOptimizer.Core.Notifications;

public static class ToastCatalog
{
    public const string RepairCompleted = "GameLoop App Market repair completed.";

    public const string OptimizationApplied = "Optimization applied.";

    public const string BackupCreated = "Backup created.";

    public const string GameLoopStarted = "GameLoop started.";

    public const string SettingsSaved = "Settings saved.";

    public static bool IsAllowed(string? message)
    {
        return message is RepairCompleted or OptimizationApplied or BackupCreated or GameLoopStarted or SettingsSaved;
    }
}

public sealed class ToastDeduper
{
    private readonly Dictionary<string, DateTimeOffset> _seen = new(StringComparer.Ordinal);
    private readonly TimeSpan _window;

    public ToastDeduper(TimeSpan? window = null)
    {
        _window = window ?? TimeSpan.FromMinutes(2);
    }

    public bool TryAccept(string? message, DateTimeOffset now)
    {
        if (!ToastCatalog.IsAllowed(message) || message is null)
        {
            return false;
        }

        if (_seen.TryGetValue(message, out var previous) && now - previous < _window)
        {
            return false;
        }

        _seen[message] = now;
        return true;
    }
}

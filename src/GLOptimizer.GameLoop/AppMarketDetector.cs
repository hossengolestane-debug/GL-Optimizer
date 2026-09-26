using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Models;

namespace GLOptimizer.GameLoop;

public sealed class AppMarketScan
{
    public IReadOnlyList<MarketInventoryItem> Items { get; init; } = [];

    public bool Truncated { get; init; }

    public int Visited { get; init; }

    public bool Completed { get; init; }
}

/// <summary>
/// Walks one verified install root. It does not follow reparse points and it stops at the depth and entry limits.
/// </summary>
public sealed class AppMarketDetector
{
    public const int DefaultMaxDepth = 6;
    public const int DefaultMaxEntries = 400;
    public const int DirectoryFileCap = 200;

    public AppMarketScan Scan(string installRoot, int maxDepth, int maxEntries, CancellationToken cancellationToken)
    {
        if (maxDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth));
        }

        if (maxEntries < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxEntries));
        }

        var root = InstallPathRules.TryNormalize(installRoot);
        if (root is null || !Directory.Exists(root))
        {
            return new AppMarketScan { Completed = false };
        }

        var items = new List<MarketInventoryItem>();
        var state = new WalkState(root, maxDepth, maxEntries, items, cancellationToken);
        state.VisitDirectory(root, 0);
        return new AppMarketScan
        {
            Items = items,
            Truncated = state.Truncated,
            Visited = state.Visited,
            Completed = !state.Failed
        };
    }

    private sealed class WalkState
    {
        private readonly string _root;
        private readonly int _maxDepth;
        private readonly int _maxEntries;
        private readonly List<MarketInventoryItem> _items;
        private readonly CancellationToken _cancellationToken;

        public WalkState(string root, int maxDepth, int maxEntries, List<MarketInventoryItem> items, CancellationToken cancellationToken)
        {
            _root = root;
            _maxDepth = maxDepth;
            _maxEntries = maxEntries;
            _items = items;
            _cancellationToken = cancellationToken;
        }

        public int Visited { get; private set; }

        public bool Truncated { get; private set; }

        public bool Failed { get; private set; }

        public void VisitDirectory(string path, int depth)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (Truncated || depth > _maxDepth || !IsUnder(path))
            {
                return;
            }

            if (IsReparse(path))
            {
                if (depth > 0 && TryCount())
                {
                    Add(path, isDirectory: true, reparse: true);
                }

                return;
            }

            if (!TryCount())
            {
                return;
            }

            if (depth > 0)
            {
                Add(path, isDirectory: true, reparse: false);
            }

            if (depth >= _maxDepth)
            {
                if (HasAnyEntry(path))
                {
                    Truncated = true;
                }

                return;
            }

            foreach (var file in Enumerate(path, directories: false))
            {
                _cancellationToken.ThrowIfCancellationRequested();
                if (Truncated)
                {
                    return;
                }

                if (IsReparse(file))
                {
                    continue;
                }

                if (!TryCount())
                {
                    return;
                }

                Add(file, isDirectory: false, reparse: false);
            }

            foreach (var directory in Enumerate(path, directories: true))
            {
                if (Truncated)
                {
                    return;
                }

                VisitDirectory(directory, depth + 1);
            }
        }

        private bool TryCount()
        {
            if (Visited >= _maxEntries)
            {
                Truncated = true;
                return false;
            }

            Visited++;
            return true;
        }

        private void Add(string path, bool isDirectory, bool reparse)
        {
            var relative = Relative(path);
            if (relative is null)
            {
                return;
            }

            var classification = AppMarketClassifier.Classify(relative, isDirectory);
            if (classification is null)
            {
                return;
            }

            var reason = reparse
                ? classification.Reason + " Reparse point was not followed."
                : classification.Reason;
            var measured = isDirectory ? MeasureDirectory(path) : MeasureFile(path);
            _items.Add(new MarketInventoryItem
            {
                Path = path,
                RelativePath = relative,
                IsDirectory = isDirectory,
                SizeBytes = measured.Size,
                FileCount = measured.Count,
                LastWriteTimeUtc = LastWrite(path),
                Kind = classification.Kind,
                Confidence = classification.Confidence,
                Reason = reason
            });
        }

        private string? Relative(string path)
        {
            string relative;
            try
            {
                relative = Path.GetRelativePath(_root, path);
            }
            catch (ArgumentException)
            {
                return null;
            }

            if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
            {
                return null;
            }

            return relative;
        }

        private bool IsUnder(string path) => InstallPathRules.IsUnderRoot(path, _root);

        private List<string> Enumerate(string path, bool directories)
        {
            try
            {
                return directories
                    ? Directory.EnumerateDirectories(path).ToList()
                    : Directory.EnumerateFiles(path).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Failed = true;
                return [];
            }
        }

        private static bool HasAnyEntry(string path)
        {
            try
            {
                return Directory.EnumerateFileSystemEntries(path).Any();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
    }

    private readonly record struct Measured(long Size, int Count);

    private static Measured MeasureFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return new Measured(info.Length < 0 ? 0 : info.Length, 1);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new Measured(0, 0);
        }
    }

    private static Measured MeasureDirectory(string path)
    {
        long size = 0;
        var count = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path))
            {
                if (count >= DirectoryFileCap)
                {
                    break;
                }

                count++;
                try
                {
                    var length = new FileInfo(file).Length;
                    if (length > 0)
                    {
                        size += length;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // This file's size is skipped. The path is still counted.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new Measured(0, 0);
        }

        return new Measured(size, count);
    }

    private static DateTimeOffset? LastWrite(string path)
    {
        try
        {
            var written = File.GetLastWriteTimeUtc(path);
            if (written.Year < 1980)
            {
                return null;
            }

            return new DateTimeOffset(DateTime.SpecifyKind(written, DateTimeKind.Utc));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static bool IsReparse(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return true;
        }
    }
}

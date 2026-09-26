namespace GLOptimizer.GameLoop;

public static class DirectoryWalker
{
    public static DirectorySearchResult FindNamed(
        string root,
        IReadOnlyCollection<string> names,
        int maxDepth,
        int maxNodes,
        CancellationToken cancellationToken)
    {
        var matches = new List<string>();
        if (string.IsNullOrWhiteSpace(root) || names.Count == 0 || maxNodes <= 0)
        {
            return new DirectorySearchResult { Matches = matches, Completed = true };
        }

        if (!Directory.Exists(root))
        {
            return new DirectorySearchResult { Matches = matches, Completed = true };
        }

        var wanted = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<(string Path, int Depth)>();
        queue.Enqueue((root, 0));
        var visited = 0;
        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (path, depth) = queue.Dequeue();
            visited++;
            if (visited > maxNodes)
            {
                return new DirectorySearchResult { Matches = matches, Completed = false };
            }

            var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (depth > 0 && wanted.Contains(name))
            {
                matches.Add(path);
            }

            if (depth >= maxDepth)
            {
                continue;
            }

            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(path);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var child in children)
            {
                try
                {
                    if (new DirectoryInfo(child).Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        continue;
                    }
                }
                catch (Exception)
                {
                    continue;
                }

                queue.Enqueue((child, depth + 1));
            }
        }

        return new DirectorySearchResult { Matches = matches, Completed = true };
    }
}

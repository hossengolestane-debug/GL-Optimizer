using Microsoft.Data.Sqlite;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Models;

namespace GLOptimizer.GameLoop;

/// <summary>
/// Reads a market version only from metadata under an AppMarket path. Package version files stay with the install.
/// SQLite is opened read-only. A missing or ambiguous value stays null.
/// </summary>
public sealed class AppMarketVersionService
{
    public string? ReadMarketVersion(string installRoot, IReadOnlyList<MarketInventoryItem> items, CancellationToken cancellationToken) =>
        ReadMarketVersion(installRoot, items, MobilePackages.Cod, cancellationToken);

    public string? ReadMarketVersion(string installRoot, IReadOnlyList<MarketInventoryItem> items, IReadOnlyList<string> packageIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(packageIds);
        var root = InstallPathRules.TryNormalize(installRoot);
        if (root is null)
        {
            return null;
        }

        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.IsDirectory || item.Kind != MarketItemKind.Metadata)
            {
                continue;
            }

            if (!InstallPathRules.IsUnderRoot(item.Path, root) || IsReparse(item.Path))
            {
                continue;
            }

            var relative = item.RelativePath.Replace('\\', '/');
            if ((!HasSegment(relative, "AppMarket") && !HasSegment(relative, "AppMarket3")) || IsPackageVersionFile(relative, packageIds))
            {
                continue;
            }

            var pathMentions = Mentions(relative, packageIds);
            string? version;
            if (IsSqlite(item.Path))
            {
                version = ReadSqlite(item.Path, pathMentions, packageIds, cancellationToken);
            }
            else if (pathMentions || TextMentions(item.Path, packageIds))
            {
                version = CodMobileVersionChecker.ReadTextVersion(item.Path);
            }
            else
            {
                version = null;
            }

            if (version is null)
            {
                continue;
            }

            found.Add(version);
            if (found.Count > 1)
            {
                return null;
            }
        }

        return found.Count == 1 ? found.First() : null;
    }

    private static string? ReadSqlite(string path, bool pathMentionsPackage, IReadOnlyList<string> packageIds, CancellationToken cancellationToken)
    {
        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };
            using var connection = new SqliteConnection(builder.ConnectionString);
            connection.Open();
            var tables = new List<string>();
            using (var list = connection.CreateCommand())
            {
                list.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%';";
                using var reader = list.ExecuteReader();
                while (reader.Read())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (reader.IsDBNull(0))
                    {
                        continue;
                    }

                    var name = reader.GetString(0);
                    if (IsIdentifier(name))
                    {
                        tables.Add(name);
                    }
                }
            }

            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (var table in tables)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var columns = ReadColumns(connection, table);
                var versionColumns = new List<string>();
                var packageColumns = new List<string>();
                foreach (var column in columns)
                {
                    if (IsVersionColumn(column))
                    {
                        versionColumns.Add(column);
                    }
                    else if (IsPackageColumn(column))
                    {
                        packageColumns.Add(column);
                    }
                }

                if (versionColumns.Count == 0 || packageColumns.Count > 1)
                {
                    continue;
                }

                if (!pathMentionsPackage && packageColumns.Count == 0)
                {
                    continue;
                }

                var packageColumn = packageColumns.Count == 1 ? packageColumns[0] : null;
                foreach (var versionColumn in versionColumns)
                {
                    ReadVersionColumn(connection, table, versionColumn, packageColumn, pathMentionsPackage, packageIds, found);
                    if (found.Count > 1)
                    {
                        return null;
                    }
                }
            }

            return found.Count == 1 ? found.First() : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static void ReadVersionColumn(
        SqliteConnection connection,
        string table,
        string versionColumn,
        string? packageColumn,
        bool pathMentionsPackage,
        IReadOnlyList<string> packageIds,
        HashSet<string> found)
    {
        using var command = connection.CreateCommand();
        command.CommandText = packageColumn is null
            ? "SELECT \"" + versionColumn + "\" FROM \"" + table + "\" LIMIT 20;"
            : "SELECT \"" + versionColumn + "\", \"" + packageColumn + "\" FROM \"" + table + "\" LIMIT 40;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (reader.IsDBNull(0))
            {
                continue;
            }

            if (packageColumn is not null)
            {
                var package = reader.IsDBNull(1) ? null : reader.GetValue(1)?.ToString();
                if (!Mentions(package, packageIds))
                {
                    continue;
                }
            }
            else if (!pathMentionsPackage)
            {
                continue;
            }

            var version = PackageVersion.Parse(reader.GetValue(0)?.ToString());
            if (version is not null)
            {
                found.Add(version.Text);
            }
        }
    }

    private static List<string> ReadColumns(SqliteConnection connection, string table)
    {
        var columns = new List<string>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM pragma_table_info('" + table + "');";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (!reader.IsDBNull(0))
            {
                var name = reader.GetString(0);
                if (IsIdentifier(name))
                {
                    columns.Add(name);
                }
            }
        }

        return columns;
    }

    private static bool TextMentions(string path, IReadOnlyList<string> packageIds)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var length = (int)Math.Min(stream.Length, 65536);
            var buffer = new byte[length];
            if (length > 0)
            {
                stream.ReadExactly(buffer);
            }

            return Mentions(System.Text.Encoding.UTF8.GetString(buffer), packageIds);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private static bool IsPackageVersionFile(string relative, IReadOnlyList<string> packageIds)
    {
        var slash = relative.LastIndexOf('/');
        if (slash <= 0)
        {
            return false;
        }

        var name = relative[(slash + 1)..];
        var parent = relative[..slash];
        var parentSlash = parent.LastIndexOf('/');
        var parentName = parentSlash < 0 ? parent : parent[(parentSlash + 1)..];
        if (!MentionsExact(parentName, packageIds))
        {
            return false;
        }

        foreach (var candidate in GameLoopLayout.VersionFileNames)
        {
            if (name.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSqlite(string path) =>
        path.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase);

    private static bool IsVersionColumn(string name) =>
        name.Equals("version", StringComparison.OrdinalIgnoreCase)
        || name.Equals("versionName", StringComparison.OrdinalIgnoreCase)
        || name.Equals("version_name", StringComparison.OrdinalIgnoreCase)
        || name.Equals("app_version", StringComparison.OrdinalIgnoreCase)
        || name.Equals("displayversion", StringComparison.OrdinalIgnoreCase);

    private static bool IsPackageColumn(string name) =>
        name.Equals("package", StringComparison.OrdinalIgnoreCase)
        || name.Equals("packageName", StringComparison.OrdinalIgnoreCase)
        || name.Equals("package_name", StringComparison.OrdinalIgnoreCase)
        || name.Equals("packagename", StringComparison.OrdinalIgnoreCase);

    private static bool IsIdentifier(string name)
    {
        if (name.Length is 0 or > 64)
        {
            return false;
        }

        foreach (var character in name)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character != '_')
            {
                return false;
            }
        }

        return true;
    }

    private static bool Mentions(string? text, IReadOnlyList<string> packageIds)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var packageId in packageIds)
        {
            if (text.Contains(packageId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MentionsExact(string name, IReadOnlyList<string> packageIds)
    {
        foreach (var packageId in packageIds)
        {
            if (name.Equals(packageId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSegment(string relative, string segment)
    {
        foreach (var part in relative.Split('/'))
        {
            if (part.Equals(segment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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

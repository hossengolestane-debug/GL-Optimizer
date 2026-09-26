using System.Text.Json;
using GLOptimizer.Core.Launch;

namespace GLOptimizer.Infrastructure.Launch;

public sealed class JsonLaunchJournalStore : ILaunchJournalStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;

    public JsonLaunchJournalStore(AppDataLocations locations)
    {
        ArgumentNullException.ThrowIfNull(locations);
        _path = Path.Combine(locations.Root, "launch-optimized.json");
    }

    public LaunchJournal? Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var journal = JsonSerializer.Deserialize<LaunchJournal>(File.ReadAllText(_path), JsonOptions);
            return journal is null || journal.Changes.Count == 0 ? null : journal;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public void Save(LaunchJournal journal)
    {
        ArgumentNullException.ThrowIfNull(journal);
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(journal, JsonOptions));
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The next startup will offer recovery again.
        }
    }
}

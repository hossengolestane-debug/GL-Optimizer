using System.Text.Json;
using GLOptimizer.Core.Abstractions;

namespace GLOptimizer.Infrastructure.Repair;

public sealed class JsonRepairStateStore : IRepairStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path;

    public JsonRepairStateStore(AppDataLocations locations)
    {
        ArgumentNullException.ThrowIfNull(locations);
        _path = Path.Combine(locations.Root, "repair-checkpoint.json");
    }

    public RepairCheckpoint? Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var checkpoint = JsonSerializer.Deserialize<RepairCheckpoint>(File.ReadAllText(_path), JsonOptions);
            return string.IsNullOrWhiteSpace(checkpoint?.BackupId) ? null : checkpoint;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public void Save(RepairCheckpoint checkpoint)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(checkpoint, JsonOptions));
    }
}

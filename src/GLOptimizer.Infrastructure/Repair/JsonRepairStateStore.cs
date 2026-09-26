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

    public string? LastProblem { get; private set; }

    public RepairCheckpoint? Load()
    {
        LastProblem = null;
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            var checkpoint = JsonSerializer.Deserialize<RepairCheckpoint>(File.ReadAllText(_path), JsonOptions);
            if (checkpoint is null || string.IsNullOrWhiteSpace(checkpoint.BackupId))
            {
                LastProblem = "The repair checkpoint could not be read. It was left in place.";
                return null;
            }

            return checkpoint;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LastProblem = "The repair checkpoint could not be read. It was left in place.";
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
        LastProblem = null;
    }

    public void Clear()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }

            LastProblem = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LastProblem = "The repair checkpoint could not be removed.";
        }
    }
}

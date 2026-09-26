using System.Text.Json;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Optimization;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Infrastructure.Optimization;

public sealed class JsonOptimizationRecordStore : IOptimizationRecordStore
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    private readonly string _path;

    public JsonOptimizationRecordStore(AppDataLocations locations)
    {
        ArgumentNullException.ThrowIfNull(locations);
        _path = Path.Combine(locations.Root, "last-optimization.json");
    }

    public OperationResult Save(OptimizationUndoRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(record, Json));
            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Failure(ex.Message);
        }
    }

    public string? LastProblem { get; private set; }

    public OperationResult<OptimizationUndoRecord> Read()
    {
        LastProblem = null;
        try
        {
            if (!File.Exists(_path))
            {
                return OperationResult<OptimizationUndoRecord>.Success(new OptimizationUndoRecord());
            }

            var record = JsonSerializer.Deserialize<OptimizationUndoRecord>(File.ReadAllText(_path), Json);
            if (record is null || string.IsNullOrWhiteSpace(record.BackupId))
            {
                LastProblem = "The last optimization record could not be read.";
                return OperationResult<OptimizationUndoRecord>.Failure(LastProblem);
            }

            return OperationResult<OptimizationUndoRecord>.Success(record);
        }
        catch (Exception)
        {
            LastProblem = "The last optimization record could not be read.";
            return OperationResult<OptimizationUndoRecord>.Failure(LastProblem);
        }
    }

    public OperationResult Clear()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }

            return OperationResult.Success();
        }
        catch (Exception ex)
        {
            return OperationResult.Failure(ex.Message);
        }
    }
}

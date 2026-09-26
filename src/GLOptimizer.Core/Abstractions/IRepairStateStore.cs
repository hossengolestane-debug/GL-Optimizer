namespace GLOptimizer.Core.Abstractions;

public sealed class RepairCheckpoint
{
    public string BackupId { get; set; } = string.Empty;

    public string? PreMarketVersion { get; set; }

    public string? InstalledVersion { get; set; }

    public DateTimeOffset CompletedAtUtc { get; set; }

    public bool AwaitingRefresh { get; set; }

    public bool InProgress { get; set; }

    public List<string> InstallRoots { get; set; } = [];
}

public interface IRepairStateStore
{
    string? LastProblem => null;

    RepairCheckpoint? Load();

    void Save(RepairCheckpoint checkpoint);

    void Clear();
}

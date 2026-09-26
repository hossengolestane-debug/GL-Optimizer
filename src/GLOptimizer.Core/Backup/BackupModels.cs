using GLOptimizer.Core.Configuration;

namespace GLOptimizer.Core.Backup;

public sealed class BackupFileEntry
{
    public string OriginalPath { get; set; } = string.Empty;

    public string StoredName { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public DateTimeOffset? LastWriteTimeUtc { get; set; }
}

public sealed class BackupManifest
{
    public string Id { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public string CreatedAtLocal { get; set; } = string.Empty;

    public string? GameLoopVersion { get; set; }

    public string Reason { get; set; } = string.Empty;

    public GameLoopSettings Settings { get; set; } = new();

    public List<BackupFileEntry> Files { get; set; } = [];

    public Dictionary<string, string> RegistryValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> UnmappedRegistryKeys { get; set; } = [];
}

public enum RestoreDisposition
{
    Replace = 0,
    Unchanged = 1,
    Skip = 2
}

public sealed class RestoreFilePlan
{
    public string StoredName { get; init; } = string.Empty;

    public required string OriginalPath { get; init; }

    public string? CurrentSha256 { get; init; }

    public string? BackupSha256 { get; init; }

    public RestoreDisposition Disposition { get; init; }

    public required string Detail { get; init; }
}

public sealed class RestorePreview
{
    public required string BackupId { get; init; }

    public IReadOnlyList<RestoreFilePlan> Files { get; init; } = [];

    public bool GameLoopRunning { get; init; }

    public bool CanRestore { get; init; }

    public string? Warning { get; init; }
}

public sealed class RestoreReport
{
    public required string BackupId { get; init; }

    public string? PreRestoreBackupId { get; init; }

    public int FilesWritten { get; init; }

    public int FilesUnchanged { get; init; }
}

public sealed class BackupDetails
{
    public required string Id { get; init; }

    public bool Damaged { get; init; }

    public string? Problem { get; init; }

    public BackupManifest? Manifest { get; init; }
}

public sealed class BackupSourceFile
{
    public required string Path { get; init; }
}

public sealed class BackupSourceSnapshot
{
    public IReadOnlyList<string> InstallRoots { get; init; } = [];

    public IReadOnlyList<string> UserDirectories { get; init; } = [];

    public IReadOnlyList<BackupSourceFile> Files { get; init; } = [];

    public GameLoopSettings Settings { get; init; } = new();

    public string? GameLoopVersion { get; init; }

    public bool GameLoopRunning { get; init; }

    public IReadOnlyDictionary<string, string> RegistryValues { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> UnmappedRegistryKeys { get; init; } = [];

    public string? Error { get; init; }

    public static BackupSourceSnapshot Failed(string error) => new() { Error = error };
}

namespace GLOptimizer.Core.Repair;

public sealed class RepairScript
{
    public IReadOnlyList<string> StopPaths { get; init; } = [];

    public string? SessionWarning { get; init; }

    public IReadOnlyList<string> BackupPaths { get; init; } = [];

    public IReadOnlyList<string> ClearPaths { get; init; } = [];

    public bool NeedsReview { get; init; }

    public string? ReviewReason { get; init; }

    public string Format()
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Would stop");
        if (StopPaths.Count == 0)
        {
            builder.AppendLine("- None. GameLoop is not running.");
        }
        else
        {
            foreach (var path in StopPaths)
            {
                builder.AppendLine("- " + path);
            }
        }

        if (!string.IsNullOrWhiteSpace(SessionWarning))
        {
            builder.AppendLine(SessionWarning);
        }

        builder.AppendLine("Would back up");
        AppendPaths(builder, BackupPaths, "- No metadata files were selected.");
        builder.AppendLine("Would clear");
        AppendPaths(builder, ClearPaths, "- No cache files were selected.");
        builder.AppendLine("Would restart");
        builder.AppendLine("Start GameLoop is offered after the repair. It is not started automatically.");
        builder.AppendLine("No game data will be removed.");
        if (NeedsReview)
        {
            builder.AppendLine("Needs review: " + (ReviewReason ?? "The repair was not started."));
        }

        return builder.ToString();
    }

    private static void AppendPaths(System.Text.StringBuilder builder, IReadOnlyList<string> paths, string empty)
    {
        if (paths.Count == 0)
        {
            builder.AppendLine(empty);
            return;
        }

        foreach (var path in paths)
        {
            builder.AppendLine("- " + path);
        }
    }
}

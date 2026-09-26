using System.Text;
using System.Text.Json;

namespace GLOptimizer.Core.Diagnostics;

public sealed class DiagnosticReportLine
{
    public required string Title { get; init; }

    public required string Detail { get; init; }

    public required string Badge { get; init; }
}

public static class DiagnosticReportBuilder
{
    public static string ToText(IReadOnlyList<DiagnosticReportLine> lines, string? userProfile)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var builder = new StringBuilder();
        builder.AppendLine("GL Optimizer diagnostic report");
        foreach (var line in lines)
        {
            builder.Append(line.Title).Append(": ").Append(line.Badge).Append(" — ").AppendLine(line.Detail);
        }

        return ReportRedaction.Redact(builder.ToString(), userProfile);
    }

    public static string ToJson(IReadOnlyList<DiagnosticReportLine> lines, string? userProfile)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var payload = lines.Select(line => new
        {
            title = line.Title,
            badge = line.Badge,
            detail = ReportRedaction.Redact(line.Detail, userProfile)
        });
        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}

using GLOptimizer.Core.Diagnostics;

namespace GLOptimizer.App.ViewModels;

public sealed record DashboardCard(string Title, string Value, string Detail, string BadgeText, StatusKind BadgeKind);

public sealed record ActivityRow(string Time, string Title, string Detail);

public sealed record DiagnosticRowModel(string Title, string Detail, string BadgeText, StatusKind BadgeKind);

public sealed record BackupRow(string Name, string When, string SizeText, string StatusText, StatusKind Status);

public sealed record LogRow(string Time, string Severity, string Category, string Message, string? Exception, StatusKind Kind);

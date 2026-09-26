using System.Globalization;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Models;

namespace GLOptimizer.App.ViewModels;

public sealed record DashboardCard(string Title, string Value, string Detail, string BadgeText, StatusKind BadgeKind);

public sealed record ActivityRow(string Time, string Title, string Detail);

public sealed record DiagnosticRowModel(string Title, string Detail, string BadgeText, StatusKind BadgeKind);

public sealed record GameStatusCard(string Title, string Subtitle, string StatusText, string Detail, string Monogram, StatusKind Kind);

public sealed record ProcessRow(string Pid, string Name, string Path);

public sealed class InstallItem
{
    private InstallItem(GameLoopInstallation installation, string path, string version, string engine, string status, string detail, StatusKind kind, IReadOnlyList<ProcessRow> processes)
    {
        Installation = installation;
        Path = path;
        Version = version;
        Engine = engine;
        Status = status;
        Detail = detail;
        Kind = kind;
        Processes = processes;
    }

    public GameLoopInstallation Installation { get; }

    public string Path { get; }

    public string Version { get; }

    public string Engine { get; }

    public string Status { get; }

    public string Detail { get; }

    public string Summary => "Version " + Version + " · Engine " + Engine;

    public StatusKind Kind { get; }

    public IReadOnlyList<ProcessRow> Processes { get; }

    public static InstallItem From(GameLoopInstallation installation)
    {
        ArgumentNullException.ThrowIfNull(installation);
        var processes = installation.Processes
            .Select(process => new ProcessRow(
                process.ProcessId.ToString(CultureInfo.InvariantCulture),
                process.ProcessName,
                process.ExecutablePath))
            .ToArray();
        return new InstallItem(
            installation,
            string.IsNullOrWhiteSpace(installation.InstallPath) ? HardwareText.Unknown : installation.InstallPath,
            HardwareText.Text(installation.Version),
            HardwareText.Text(installation.Engine),
            DetectionText.RunStatus(installation.RunStatus),
            string.IsNullOrWhiteSpace(installation.LauncherPath) ? "Launcher unknown." : installation.LauncherPath,
            DetectionText.RunKind(installation.RunStatus),
            processes);
    }
}

public sealed record BackupRow(string Name, string When, string SizeText, string StatusText, StatusKind Status);

public sealed record LogRow(string Time, string Severity, string Category, string Message, string? Exception, StatusKind Kind);

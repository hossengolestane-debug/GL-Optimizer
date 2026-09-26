using System.Globalization;
using System.Text;
using GLOptimizer.Core.Configuration;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;

namespace GLOptimizer.App.ViewModels;

public static class ConfigRows
{
    public static void FillSettings(IList<DiagnosticRowModel> rows, GameLoopSettings? settings)
    {
        rows.Clear();
        rows.Add(Setting("Renderer", settings?.Renderer));
        rows.Add(Setting("Resolution", settings?.Resolution));
        rows.Add(Setting("DPI", settings?.Dpi));
        rows.Add(Setting("Memory", settings?.MemoryAllocation));
        rows.Add(Setting("CPU allocation", settings?.CpuAllocation));
        rows.Add(Setting("VSync", settings?.VSync));
        rows.Add(Setting("Anti-aliasing", settings?.AntiAliasing));
        rows.Add(Setting("FPS target", settings?.FpsTarget));
    }

    public static DiagnosticRowModel File(ConfigFileRecord file)
    {
        var detail = new StringBuilder(file.Path);
        if (file.SizeBytes is long size)
        {
            detail.Append("  ·  ").Append(size.ToString(CultureInfo.InvariantCulture)).Append(" bytes");
        }

        if (file.LastWriteTime is DateTimeOffset written)
        {
            detail.Append("  ·  ").Append(written.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(file.Detail))
        {
            detail.Append("  ·  ").Append(file.Detail);
        }

        var (badge, kind) = file.Presence switch
        {
            ConfigPresence.Present => ("Found", StatusKind.Ready),
            ConfigPresence.Unreadable => ("Unreadable", StatusKind.Attention),
            _ => ("Not found", StatusKind.Unavailable)
        };
        var title = file.Kind switch
        {
            ConfigFileKind.KeyMap => "Key map",
            ConfigFileKind.Registry => "Registry",
            ConfigFileKind.UserSettings => "User config",
            _ => "Config file"
        };
        return new DiagnosticRowModel(title, detail.ToString(), badge, kind);
    }

    private static DiagnosticRowModel Setting(string title, string? value)
    {
        var known = !string.IsNullOrWhiteSpace(value);
        return new DiagnosticRowModel(
            title,
            known ? value! : HardwareText.Unknown,
            known ? "Reported" : "Unknown",
            known ? StatusKind.Ready : StatusKind.Unavailable);
    }
}

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Navigation;

namespace GLOptimizer.App.ViewModels;

public partial class AppMarketViewModel : PageViewModel, IRefreshable
{
    private readonly IAppMarketDiagnostics _diagnostics;
    private readonly ScanSession _session = new();

    public AppMarketViewModel(IAppMarketDiagnostics diagnostics)
        : base(AppPage.AppMarket)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        _diagnostics = diagnostics;
    }

    public string SafetyNote => Phase0Notices.Safety + " " + Phase0Notices.NoAppMarketIo;

    public string RepairNote =>
        "Nothing is changed now. DRY RUN REPAIR and REPAIR APP MARKET are available in Phase 8, and a repair would be limited to allowlisted cache items.";

    public ObservableCollection<MarketItemRow> Inventory { get; } = new();

    public ObservableCollection<MarketItemRow> RepairTargets { get; } = new();

    [ObservableProperty]
    private string _statusText = "UNKNOWN";

    [ObservableProperty]
    private string _installedVersion = "Unknown";

    [ObservableProperty]
    private string _marketVersion = "Unknown";

    [ObservableProperty]
    private string _officialVersion = "Unknown";

    [ObservableProperty]
    private string _officialDetail = "Not requested. Check Version on the COD Mobile page is the only control that contacts a host.";

    [ObservableProperty]
    private string _lastScan = "Unknown";

    [ObservableProperty]
    private string _issue = "Scanning…";

    public void Refresh() => _ = ScanAsync();

    [RelayCommand]
    private Task CheckAgainAsync() => ScanAsync();

    private async Task ScanAsync()
    {
        var (generation, token) = _session.Start();
        try
        {
            Issue = "Scanning…";
            var result = await _diagnostics.ScanAsync(checkOfficialVersion: false, token);
            if (!_session.IsCurrent(generation))
            {
                return;
            }

            Inventory.Clear();
            RepairTargets.Clear();
            if (!result.Succeeded || result.Value is null)
            {
                StatusText = "UNKNOWN";
                InstalledVersion = "Unknown";
                MarketVersion = "Unknown";
                OfficialVersion = "Unknown";
                OfficialDetail = "Not requested.";
                LastScan = "Unknown";
                Issue = result.Error ?? "App Market could not be scanned.";
                return;
            }

            var report = result.Value;
            StatusText = report.StatusText;
            InstalledVersion = Text(report.InstalledVersion);
            MarketVersion = Text(report.MarketVersion);
            OfficialVersion = Text(report.OfficialVersion);
            OfficialDetail = report.OfficialDetail ?? "Not requested.";
            LastScan = report.LastScanUtc?.ToString("u", CultureInfo.InvariantCulture) ?? "Unknown";
            Issue = report.Issue ?? report.Detail ?? "No issue text was recorded.";
            if (report.ScanTruncated)
            {
                Issue += " The inventory stopped at the scan limit.";
            }

            foreach (var item in report.Inventory)
            {
                Inventory.Add(MarketItemRow.From(item));
            }

            foreach (var item in report.RepairTargets)
            {
                RepairTargets.Add(MarketItemRow.From(item));
            }
        }
        catch (Exception ex)
        {
            if (_session.IsCurrent(generation))
            {
                StatusText = "UNKNOWN";
                Issue = "The scan could not finish. " + ex.Message;
            }
        }
    }

    private static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
}

public sealed record MarketItemRow(string Path, string Kind, string Confidence, string Size, string Written, string Reason)
{
    public static MarketItemRow From(Core.Models.MarketInventoryItem item)
    {
        var written = item.LastWriteTimeUtc?.ToString("u", CultureInfo.InvariantCulture) ?? "Unknown";
        var size = item.SizeBytes.ToString(CultureInfo.InvariantCulture) + " bytes, "
            + item.FileCount.ToString(CultureInfo.InvariantCulture) + " file(s)";
        return new MarketItemRow(item.RelativePath, item.Kind.ToString(), item.Confidence.ToString(), size, written, item.Reason);
    }
}

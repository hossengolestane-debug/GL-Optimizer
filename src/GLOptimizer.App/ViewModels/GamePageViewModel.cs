using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Navigation;

namespace GLOptimizer.App.ViewModels;

public partial class GamePageViewModel : PageViewModel, IRefreshable
{
    private readonly IGameLoopDetector _detector;
    private readonly ICodMobileDiagnostics _cod;
    private readonly ScanSession _session = new();

    public GamePageViewModel(IGameLoopDetector detector, ICodMobileDiagnostics cod, AppPage page)
        : base(page)
    {
        if (page is not (AppPage.CodMobile or AppPage.PubgMobile))
        {
            throw new ArgumentOutOfRangeException(nameof(page), page, "This page is only used for game sections.");
        }

        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(cod);
        _detector = detector;
        _cod = cod;
        Monogram = Title.Length == 0 ? "?" : char.ToUpperInvariant(Title[0]).ToString();
        IsCodPage = page == AppPage.CodMobile;
    }

    public string Monogram { get; }

    public bool IsCodPage { get; }

    public string SafetyNote => Phase0Notices.Safety + " " + Phase0Notices.ReadOnlyGameLoop;

    public ObservableCollection<FindingRow> Findings { get; } = new();

    [ObservableProperty]
    private string _statusText = "Unknown";

    [ObservableProperty]
    private string _cardDetail = "Scanning…";

    [ObservableProperty]
    private StatusKind _kind = StatusKind.Unavailable;

    [ObservableProperty]
    private string _installedVersion = "Unknown";

    [ObservableProperty]
    private string _marketVersion = "Unknown";

    [ObservableProperty]
    private string _officialVersion = "Unknown";

    [ObservableProperty]
    private string _officialDetail = "Not requested. Check Version is the only control that contacts a host.";

    [ObservableProperty]
    private string _comparisonText = "UNKNOWN";

    public void Refresh() => _ = IsCodPage ? RunCodAsync(false) : RunPubgAsync();

    [RelayCommand]
    private Task CheckVersionAsync() => RunCodAsync(true);

    [RelayCommand]
    private Task RunDiagnosticsAsync() => RunCodAsync(false);

    private async Task RunPubgAsync()
    {
        var (generation, token) = _session.Start();
        try
        {
            StatusText = "Unknown";
            CardDetail = "Scanning…";
            Kind = StatusKind.Neutral;
            var result = await _detector.DetectAsync(token);
            if (!_session.IsCurrent(generation))
            {
                return;
            }

            if (!result.Succeeded || result.Value is null)
            {
                StatusText = "Unknown";
                CardDetail = result.Error ?? "GameLoop could not be scanned.";
                Kind = StatusKind.Unavailable;
                return;
            }

            ApplyPresence(result.Value.PubgMobile);
        }
        catch (Exception ex)
        {
            if (_session.IsCurrent(generation))
            {
                StatusText = "Unknown";
                CardDetail = "The scan could not finish. " + ex.Message;
                Kind = StatusKind.Unavailable;
            }
        }
    }

    private async Task RunCodAsync(bool checkOfficialVersion)
    {
        if (!IsCodPage)
        {
            return;
        }

        var (generation, token) = _session.Start();
        try
        {
            StatusText = "Unknown";
            ComparisonText = "UNKNOWN";
            CardDetail = checkOfficialVersion ? "Checking version…" : "Collecting diagnostics…";
            Kind = StatusKind.Neutral;
            var result = await _cod.RunAsync(checkOfficialVersion, token);
            if (!_session.IsCurrent(generation))
            {
                return;
            }

            Findings.Clear();
            if (!result.Succeeded || result.Value is null)
            {
                StatusText = "Unknown";
                InstalledVersion = "Unknown";
                MarketVersion = "Unknown";
                OfficialVersion = "Unknown";
                OfficialDetail = result.Error ?? "COD Mobile could not be scanned.";
                CardDetail = OfficialDetail;
                Kind = StatusKind.Unavailable;
                return;
            }

            var report = result.Value;
            var market = report.Market;
            InstalledVersion = Display(market.InstalledVersion);
            MarketVersion = Display(market.MarketVersion);
            OfficialVersion = Display(market.OfficialVersion);
            OfficialDetail = market.OfficialDetail ?? "Unknown";
            ComparisonText = market.StatusText;
            StatusText = market.StatusText;
            Kind = KindFor(market.Comparison);
            CardDetail = market.Issue ?? report.InstalledDetail ?? "Unknown";
            if (!string.IsNullOrWhiteSpace(report.PackagePath))
            {
                CardDetail += " " + report.PackagePath;
            }

            foreach (var finding in report.Findings)
            {
                Findings.Add(new FindingRow(
                    finding.Title,
                    finding.Evidence,
                    finding.RecommendedAction,
                    OutcomeText(finding.Outcome)));
            }
        }
        catch (Exception ex)
        {
            if (!_session.IsCurrent(generation))
            {
                return;
            }

            StatusText = "Unknown";
            CardDetail = "The scan could not finish. " + ex.Message;
            Kind = StatusKind.Unavailable;
        }
    }

    private void ApplyPresence(MobileGamePresence presence)
    {
        var status = DetectionText.ForMobile(presence, false);
        StatusText = status.Badge;
        Kind = status.Kind;
        CardDetail = presence.Detail ?? (presence.Version is null ? "Version unknown." : "Version " + presence.Version);
        if (!string.IsNullOrWhiteSpace(presence.Path))
        {
            CardDetail += " " + presence.Path;
        }
    }

    private static string Display(string? version) => string.IsNullOrWhiteSpace(version) ? "Unknown" : version;

    private static StatusKind KindFor(CatalogComparison comparison) => comparison switch
    {
        CatalogComparison.Match => StatusKind.Ready,
        CatalogComparison.VersionMismatch => StatusKind.Attention,
        CatalogComparison.LocalMarketOutdated => StatusKind.Attention,
        CatalogComparison.RemoteCatalogIssue => StatusKind.Attention,
        _ => StatusKind.Unavailable
    };

    private static string OutcomeText(FindingOutcome outcome) => outcome switch
    {
        FindingOutcome.Pass => "PASS",
        FindingOutcome.Warning => "WARNING",
        FindingOutcome.Failed => "FAILED",
        _ => "UNKNOWN"
    };
}

public sealed record FindingRow(string Title, string Evidence, string Action, string Outcome);

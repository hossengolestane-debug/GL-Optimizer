using CommunityToolkit.Mvvm.ComponentModel;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Navigation;

namespace GLOptimizer.App.ViewModels;

public partial class GamePageViewModel : PageViewModel, IRefreshable
{
    private readonly IGameLoopDetector _detector;
    private readonly ScanSession _session = new();

    public GamePageViewModel(IGameLoopDetector detector, AppPage page)
        : base(page)
    {
        if (page is not (AppPage.CodMobile or AppPage.PubgMobile))
        {
            throw new ArgumentOutOfRangeException(nameof(page), page, "This page is only used for game sections.");
        }

        _detector = detector;
        Monogram = Title.Length == 0 ? "?" : char.ToUpperInvariant(Title[0]).ToString();
    }

    public string Monogram { get; }

    public string SafetyNote => Phase0Notices.Safety + " " + Phase0Notices.ReadOnlyGameLoop;

    [ObservableProperty]
    private string _statusText = "Unknown";

    [ObservableProperty]
    private string _cardDetail = "Scanning…";

    [ObservableProperty]
    private StatusKind _kind = StatusKind.Unavailable;

    public void Refresh() => _ = RunScanAsync();

    private async Task RunScanAsync()
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

            var presence = Page == AppPage.PubgMobile ? result.Value.PubgMobile : result.Value.CodMobile;
            var status = DetectionText.ForMobile(presence, false);
            StatusText = status.Badge;
            Kind = status.Kind;
            CardDetail = presence.Detail
                ?? (presence.Version is null ? "Version unknown." : "Version " + presence.Version);
            if (!string.IsNullOrWhiteSpace(presence.Path))
            {
                CardDetail += " " + presence.Path;
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
}

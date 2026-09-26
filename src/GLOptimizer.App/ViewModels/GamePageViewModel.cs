using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Navigation;

namespace GLOptimizer.App.ViewModels;

public partial class GamePageViewModel : PageViewModel
{
    public GamePageViewModel(AppPage page)
        : base(page)
    {
        if (page is not (AppPage.CodMobile or AppPage.PubgMobile))
        {
            throw new ArgumentOutOfRangeException(nameof(page), page, "This page is only used for game sections.");
        }

        Monogram = Title.Length == 0 ? "?" : char.ToUpperInvariant(Title[0]).ToString();
    }

    public string Monogram { get; }

    public string StatusText => "Not available";

    public string CardDetail => "No profile, install path, or performance data is loaded.";

    public string SafetyNote => Phase0Notices.Safety;
}

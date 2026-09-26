using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Diagnostics;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.GameLoop;

public sealed class CodMobileDiagnostics : ICodMobileDiagnostics
{
    private readonly CodMobileDetector _detector;
    private readonly IAppMarketDiagnostics _market;
    private readonly CodMobileLaunchDiagnostics _launch;

    public CodMobileDiagnostics(CodMobileDetector detector, IAppMarketDiagnostics market, CodMobileLaunchDiagnostics launch)
    {
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(market);
        ArgumentNullException.ThrowIfNull(launch);
        _detector = detector;
        _market = market;
        _launch = launch;
    }

    public async Task<OperationResult<CodMobileReport>> RunAsync(bool checkOfficialVersion, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return OperationResult<CodMobileReport>.Failure("The scan was cancelled.");
        }

        try
        {
            var presence = await _detector.DetectAsync(cancellationToken).ConfigureAwait(false);
            var market = await _market.ScanAsync(checkOfficialVersion, cancellationToken).ConfigureAwait(false);
            if (!market.Succeeded || market.Value is null)
            {
                return OperationResult<CodMobileReport>.Failure(market.Error ?? presence.Error ?? "COD Mobile could not be scanned.");
            }

            var report = market.Value;
            var evidence = await _launch.CollectAsync(
                report.InstallPaths.Count > 0 ? report.InstallPaths : presence.InstallPaths,
                report.Inventory,
                presence.Presence.Path ?? report.PackagePath,
                report.Comparison,
                report.Issue,
                cancellationToken).ConfigureAwait(false);
            var findings = GrayScreenAnalyzer.Rank(evidence);
            return OperationResult<CodMobileReport>.Success(new CodMobileReport
            {
                InstalledStatus = presence.Succeeded ? presence.Presence.Status : report.InstalledStatus,
                InstalledDetail = presence.Succeeded ? presence.Presence.Detail : report.Detail,
                PackagePath = presence.Presence.Path ?? report.PackagePath,
                Market = report,
                Findings = findings
            });
        }
        catch (OperationCanceledException)
        {
            return OperationResult<CodMobileReport>.Failure("The scan was cancelled.");
        }
        catch (Exception)
        {
            return OperationResult<CodMobileReport>.Failure("COD Mobile diagnostics could not finish.");
        }
    }
}

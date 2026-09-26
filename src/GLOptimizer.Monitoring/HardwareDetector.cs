using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Detection;
using GLOptimizer.Core.Models;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Monitoring;

public sealed class HardwareDetector : IHardwareService
{
    private readonly IHardwareProbe _probe;

    public HardwareDetector(IHardwareProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        _probe = probe;
    }

    public async Task<OperationResult<HardwareReport>> GetReportAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return OperationResult<HardwareReport>.Failure("The hardware scan was cancelled.");
        }

        try
        {
            var snapshot = await Task.Run(() => _probe.Capture(cancellationToken), cancellationToken).ConfigureAwait(false);
            return OperationResult<HardwareReport>.Success(HardwareReportBuilder.Build(snapshot));
        }
        catch (OperationCanceledException)
        {
            return OperationResult<HardwareReport>.Failure("The hardware scan was cancelled.");
        }
        catch (Exception)
        {
            return OperationResult<HardwareReport>.Failure("Hardware could not be read.");
        }
    }
}

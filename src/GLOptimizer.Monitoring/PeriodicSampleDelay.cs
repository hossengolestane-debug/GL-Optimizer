using GLOptimizer.Core.Abstractions;

namespace GLOptimizer.Monitoring;

public sealed class PeriodicSampleDelay : ISampleDelay
{
    public async Task WaitAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        if (interval < TimeSpan.FromMilliseconds(1))
        {
            interval = TimeSpan.FromMilliseconds(1);
        }

        using var timer = new PeriodicTimer(interval);
        await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
    }
}

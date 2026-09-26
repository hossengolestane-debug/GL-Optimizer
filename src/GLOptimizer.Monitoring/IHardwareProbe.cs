using GLOptimizer.Core.Models;

namespace GLOptimizer.Monitoring;

public interface IHardwareProbe
{
    HardwareProbeSnapshot Capture(CancellationToken cancellationToken);
}

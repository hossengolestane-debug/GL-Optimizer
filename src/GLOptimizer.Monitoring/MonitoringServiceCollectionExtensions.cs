using GLOptimizer.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace GLOptimizer.Monitoring;

public static class MonitoringServiceCollectionExtensions
{
    public static IServiceCollection AddMonitoringModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IHardwareProbe, WindowsHardwareProbe>();
        services.AddSingleton<IHardwareService, HardwareDetector>();
        services.AddSingleton<IFrameMetricsProvider, NotImplementedFrameMetricsProvider>();
        return services;
    }
}

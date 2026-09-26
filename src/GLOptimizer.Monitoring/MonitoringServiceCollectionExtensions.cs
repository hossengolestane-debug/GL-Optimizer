using GLOptimizer.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace GLOptimizer.Monitoring;

public static class MonitoringServiceCollectionExtensions
{
    public static IServiceCollection AddMonitoringModule(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IHardwareService, NotImplementedHardwareService>();
        services.AddSingleton<IFrameMetricsProvider, NotImplementedFrameMetricsProvider>();
        return services;
    }
}

using GLOptimizer.Core.Network;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Core.Abstractions;

public interface INetworkStatus
{
    bool IsAvailable { get; }

    string ConnectionType { get; }
}

public interface INetworkProbe
{
    Task<OperationResult<int>> ProbeAsync(string host, int port, CancellationToken cancellationToken = default);
}

public interface INetworkDiagnostics
{
    Task<OperationResult<NetworkReport>> RunAsync(CancellationToken cancellationToken = default);
}

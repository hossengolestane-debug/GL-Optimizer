using System.Diagnostics;
using System.Net.Sockets;
using GLOptimizer.Core.Abstractions;
using GLOptimizer.Core.Network;
using GLOptimizer.Core.Results;

namespace GLOptimizer.Infrastructure.Network;

public sealed class NetworkInterfaceStatus : INetworkStatus
{
    public bool IsAvailable { get; init; }

    public string ConnectionType { get; init; } = "Unknown";

    public static NetworkInterfaceStatus Capture()
    {
        try
        {
            foreach (var adapter in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
                {
                    continue;
                }

                if (adapter.NetworkInterfaceType is System.Net.NetworkInformation.NetworkInterfaceType.Loopback
                    or System.Net.NetworkInformation.NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                return new NetworkInterfaceStatus
                {
                    IsAvailable = true,
                    ConnectionType = adapter.NetworkInterfaceType.ToString()
                };
            }
        }
        catch (Exception)
        {
            return new NetworkInterfaceStatus();
        }

        return new NetworkInterfaceStatus { ConnectionType = "Unavailable" };
    }
}

public sealed class LiveNetworkStatus : INetworkStatus
{
    public bool IsAvailable => NetworkInterfaceStatus.Capture().IsAvailable;

    public string ConnectionType => NetworkInterfaceStatus.Capture().ConnectionType;
}

public sealed class TcpConnectProbe : INetworkProbe
{
    public async Task<OperationResult<int>> ProbeAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        if (!NetworkAllowlist.IsAllowed(host) || port != NetworkAllowlist.Port)
        {
            return OperationResult<int>.Failure("That endpoint is not on the network allowlist.");
        }

        var watch = Stopwatch.StartNew();
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            await client.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);
            watch.Stop();
            return OperationResult<int>.Success((int)watch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            return OperationResult<int>.Failure("The network check was cancelled.");
        }
        catch (Exception)
        {
            return OperationResult<int>.Failure("The endpoint did not answer.");
        }
    }
}

public sealed class NetworkDiagnosticsService : INetworkDiagnostics
{
    private readonly INetworkStatus _status;
    private readonly INetworkProbe _probe;

    public NetworkDiagnosticsService(INetworkStatus status, INetworkProbe probe)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(probe);
        _status = status;
        _probe = probe;
    }

    public async Task<OperationResult<NetworkReport>> RunAsync(CancellationToken cancellationToken = default)
    {
        var samples = new List<NetworkSample>();
        foreach (var host in NetworkAllowlist.Hosts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var probed = await _probe.ProbeAsync(host, NetworkAllowlist.Port, cancellationToken).ConfigureAwait(false);
            samples.Add(new NetworkSample
            {
                Host = host,
                LatencyMilliseconds = probed.Succeeded ? probed.Value : null,
                Error = probed.Succeeded ? null : probed.Error
            });
        }

        return OperationResult<NetworkReport>.Success(new NetworkReport
        {
            Available = _status.IsAvailable,
            ConnectionType = string.IsNullOrWhiteSpace(_status.ConnectionType) ? "Unknown" : _status.ConnectionType,
            RequestedNetwork = true,
            Samples = samples
        });
    }
}

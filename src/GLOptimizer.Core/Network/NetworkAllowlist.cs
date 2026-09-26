namespace GLOptimizer.Core.Network;

public static class NetworkAllowlist
{
    public const int Port = 443;

    public static IReadOnlyList<string> Hosts { get; } = ["one.one.one.one", "dns.google"];

    public static bool IsAllowed(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        foreach (var allowed in Hosts)
        {
            if (host.Equals(allowed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

public sealed class NetworkSample
{
    public required string Host { get; init; }

    public int? LatencyMilliseconds { get; init; }

    public string? Error { get; init; }
}

public sealed class NetworkReport
{
    public bool Available { get; init; }

    public string ConnectionType { get; init; } = "Unknown";

    public bool RequestedNetwork { get; init; }

    public IReadOnlyList<NetworkSample> Samples { get; init; } = [];
}

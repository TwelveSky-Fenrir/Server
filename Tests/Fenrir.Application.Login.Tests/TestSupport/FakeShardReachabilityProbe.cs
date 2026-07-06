using Fenrir.Application.Login.Abstractions.ZoneTransfer;

namespace Fenrir.Application.Login.Tests.TestSupport;

// In-memory stand-in for IShardReachabilityProbe: defaults to "always reachable" so every existing
// success-path test doesn't have to fake a real TCP connect; mark specific host:port pairs unreachable (or
// flip Reachable for all of them) to exercise ZoneTransferService's dead-shard-eviction rejection path.
internal sealed class FakeShardReachabilityProbe : IShardReachabilityProbe
{
    private readonly HashSet<(string Host, int Port)> _unreachableHostPorts = [];

    /// <summary>Every (Host, Port) IsReachableAsync was queried with, in call order.</summary>
    public List<(string Host, int Port)> ProbedHostPorts { get; } = [];

    /// <summary>Global switch: when false, every probe fails regardless of <see cref="MarkUnreachable" />.</summary>
    public bool Reachable { get; set; } = true;

    public ValueTask<bool> IsReachableAsync(string host, int port, CancellationToken ct)
    {
        ProbedHostPorts.Add((host, port));
        var reachable = Reachable && !_unreachableHostPorts.Contains((host, port));
        return ValueTask.FromResult(reachable);
    }

    /// <summary>Fails the probe for one specific shard address only, leaving every other address reachable.</summary>
    public FakeShardReachabilityProbe MarkUnreachable(string host, int port)
    {
        _unreachableHostPorts.Add((host, port));
        return this;
    }
}

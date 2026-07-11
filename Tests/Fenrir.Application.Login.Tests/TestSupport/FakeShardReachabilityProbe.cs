using Fenrir.Application.Login.Abstractions.ZoneTransfer;

namespace Fenrir.Application.Login.Tests.TestSupport;

internal sealed class FakeShardReachabilityProbe : IShardReachabilityProbe
{
    private readonly HashSet<(string Host, int Port)> _unreachableHostPorts = [];

        public List<(string Host, int Port)> ProbedHostPorts { get; } = [];

        public bool Reachable { get; set; } = true;

    public ValueTask<bool> IsReachableAsync(string host, int port, CancellationToken ct)
    {
        ProbedHostPorts.Add((host, port));
        var reachable = Reachable && !_unreachableHostPorts.Contains((host, port));
        return ValueTask.FromResult(reachable);
    }

        public FakeShardReachabilityProbe MarkUnreachable(string host, int port)
    {
        _unreachableHostPorts.Add((host, port));
        return this;
    }
}

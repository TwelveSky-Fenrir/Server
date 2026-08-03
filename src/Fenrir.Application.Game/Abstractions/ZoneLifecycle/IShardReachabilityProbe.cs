namespace Fenrir.Application.Game.Abstractions.ZoneLifecycle;

public interface IShardReachabilityProbe
{
    public ValueTask<bool> IsReachableAsync(string host, int port, CancellationToken ct);
}

namespace Fenrir.Application.Login.Abstractions.ZoneTransfer;

public interface IShardReachabilityProbe
{
    public ValueTask<bool> IsReachableAsync(string host, int port, CancellationToken ct);
}

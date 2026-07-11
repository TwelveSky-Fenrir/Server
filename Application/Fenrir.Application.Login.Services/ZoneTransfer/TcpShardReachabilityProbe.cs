using System.Net.Sockets;
using Fenrir.Application.Login.Abstractions.ZoneTransfer;
using Fenrir.Application.Login.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Services.ZoneTransfer;

public sealed class TcpShardReachabilityProbe(
    IOptions<LoginServerOptions> options,
    ILogger<TcpShardReachabilityProbe> logger) : IShardReachabilityProbe
{
    public async ValueTask<bool> IsReachableAsync(string host, int port, CancellationToken ct)
    {
        using var client = new TcpClient();
        using var timeoutCts =
            new CancellationTokenSource(
                TimeSpan.FromMilliseconds(options.Value.ShardReachabilityProbeTimeoutMilliseconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            await client.ConnectAsync(host, port, linkedCts.Token).ConfigureAwait(false);
            return true;
        }
        catch (SocketException ex)
        {
            logger.LogDebug(ex, "Shard reachability probe: TCP connect to {Host}:{Port} failed", host, port);
            return false;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            logger.LogDebug(
                "Shard reachability probe: TCP connect to {Host}:{Port} timed out after {TimeoutMs}ms", host,
                port, options.Value.ShardReachabilityProbeTimeoutMilliseconds);
            return false;
        }
    }
}

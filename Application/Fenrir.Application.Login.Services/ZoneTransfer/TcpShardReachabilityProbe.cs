using System.Net.Sockets;
using Fenrir.Application.Login.Abstractions.ZoneTransfer;
using Fenrir.Application.Login.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Login.Services.ZoneTransfer;

/// <summary>
///     Real <see cref="IShardReachabilityProbe" />: a bare TCP connect attempt, torn down immediately
///     whether it succeeds or fails. This never speaks the wire protocol (no handshake, no bytes
///     exchanged) -- <see cref="ZoneTransferService" /> only needs a liveness signal before minting a
///     ticket, not a protocol-level health check.
/// </summary>
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
            // Refused, unreachable, DNS failure, etc. -- the shard is not accepting connections right now.
            logger.LogDebug(ex, "Shard reachability probe: TCP connect to {Host}:{Port} failed", host, port);
            return false;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Our own probe timeout fired (linkedCts), not the caller's cancellation -- a non-responding
            // socket is treated exactly like a refused one: unreachable, not an error.
            logger.LogDebug(
                "Shard reachability probe: TCP connect to {Host}:{Port} timed out after {TimeoutMs}ms", host,
                port, options.Value.ShardReachabilityProbeTimeoutMilliseconds);
            return false;
        }
    }
}

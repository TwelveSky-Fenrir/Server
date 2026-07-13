using Fenrir.Cluster.WorldState;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.CenterServer.Hosting;

/// <summary>
///     The idle-peer sweep: periodically disconnects every connected S2S peer (zone/login link) whose real
///     elapsed time since last activity exceeds 3 minutes of wall-clock.
/// </summary>
/// <remarks>
///     Reimplemented from the CenterServer idle sweep (Server/ts25center/S07_MyGame01.cpp:205-216). The 3-minute
///     threshold is a real-time value (<c>GetMinuteFromTick(3)</c> = 180000 ms), not a beat count. The actual
///     peer set and per-peer last-activity timestamps are owned by Main / the session-and-link unit behind
///     <see cref="ICenterPeerRegistry" />; this host only drives the cadence.
/// </remarks>
public sealed class PeerLivenessHost(
    ICenterPeerRegistry peers,
    ILogger<PeerLivenessHost> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan IdleThreshold = TimeSpan.FromMinutes(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);

        do
        {
            try
            {
                var dropped = peers.DisconnectIdlePeers(IdleThreshold, DateTimeOffset.UtcNow);
                if (dropped > 0)
                    logger.LogInformation("Peer liveness sweep disconnected {Count} idle S2S peer(s) (>3 min)", dropped);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Peer liveness sweep failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}

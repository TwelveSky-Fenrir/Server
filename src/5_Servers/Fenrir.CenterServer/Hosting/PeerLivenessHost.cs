using Fenrir.Cluster.WorldState;

namespace Fenrir.CenterServer.Hosting;

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
                    logger.LogInformation("Peer liveness sweep disconnected {Count} idle S2S peer(s) (>3 min)",
                        dropped);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Peer liveness sweep failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}

using System.Buffers.Binary;
using Fenrir.Cluster.WorldState;
using Fenrir.Data.Abstractions.Progression;

namespace Fenrir.CenterServer.Hosting;

public sealed class HeroRankRolloverHost(
    IHeroRankingRepository heroRankings,
    IWorldStateAuthority worldState,
    ICenterLinkBroadcaster broadcaster,
    ILogger<HeroRankRolloverHost> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        do
        {
            try
            {
                if (await heroRankings.RolloverIfDueAsync(stoppingToken).ConfigureAwait(false))
                {
                    logger.LogInformation("Hero-rank weekly rollover fired (7-day sentinel elapsed)");
                    await RotateAdvantagedTribeAsync(stoppingToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Hero-rank rollover check failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task RotateAdvantagedTribeAsync(CancellationToken ct)
    {
        var next = FavoredTribeRankLadder.NextFavoredTribe(worldState.HighTribe);
        worldState.SetHighTribe(next);

        var totals = FavoredTribeRankLadder.ComputeTotals(next);
        worldState.OverwriteTribePoints(totals);

        var payload = new byte[CenterWorldEventSorts.PayloadSize];
        for (var i = 0; i < totals.Length && i < 4; i++)
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(i * 4, 4), totals[i]);

        try
        {
            await broadcaster.BroadcastWorldEventAsync(CenterWorldEventSorts.TribePointSync, payload, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Advantaged-tribe rotation broadcast (1234) failed");
        }

        logger.LogInformation(
            "Advantaged tribe rotated to {NextTribe} (Bug 2 fix): scores {T0}/{T1}/{T2}/{T3}",
            next, totals[0], totals[1], totals[2], totals[3]);
    }
}

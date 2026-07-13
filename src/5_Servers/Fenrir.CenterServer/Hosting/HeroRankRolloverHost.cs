using System.Buffers.Binary;
using Fenrir.Cluster.WorldState;
using Fenrir.Data.Abstractions.Progression;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.CenterServer.Hosting;

/// <summary>
///     The 7-day hero-rank rollover cadence, and (Bug 2 fix) the advantaged-tribe rotation on the same weekly
///     cadence. The 7-day gate itself lives in <c>usp_HeroRanking_Rollover</c> (a real-calendar DATEDIFF against
///     the sentinel row), so this host only needs to poll it periodically; when a rollover actually fires, the
///     advantaged tribe advances 0 -&gt; 1 -&gt; 2 -&gt; 3 -&gt; 0, the four scores are recomputed and broadcast.
/// </summary>
/// <remarks>
///     Reimplemented from the weekly rollover (Server/ts25center/S08_MyDB.cpp:348-467) and CORRECTS Bug 2: the
///     legacy advance-the-week rotation call after the rollover was commented out (S08_MyDB.cpp:445-447), so the
///     advantaged tribe never rotated automatically. Here it runs on every weekly flip.
///     <para>
///         DORMANCY NOTE: the GameServer also polls <c>usp_HeroRanking_Rollover</c> this lot; the proc is
///         serialised on its sentinel row so only one caller wins the flip. If a shard wins the race, this host
///         sees <c>false</c> and the advantaged-tribe rotation is skipped for that week -- acceptable while
///         dormant (Bug 2 is Center-only anyway); Lot 5 makes the Center the sole caller.
///     </para>
/// </remarks>
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

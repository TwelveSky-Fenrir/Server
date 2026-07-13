using System.Buffers.Binary;
using Fenrir.Cluster.WorldState;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.CenterServer.Hosting;

/// <summary>
///     The ~6s real-time aggregate-flush cadence for the Center authoritative world state. Runs, in the legacy
///     order: flush world/tribe/alliance, recompute the four tribe scores from the character population (Bug 3),
///     flush hero-rank accrual and the tower store, then poll the operator-driven tribe-point recompute flag and
///     broadcast the 1234 sync when consumed. Also performs the mandatory final flush on stop.
/// </summary>
/// <remarks>
///     Reimplemented from the 6-beat block (Server/ts25center/S07_MyGame01.cpp:240-267). The cadence is derived
///     from wall-clock time via <see cref="PeriodicTimer" />, never a loop counter. Ordering nuance preserved:
///     the world/tribe flush runs BEFORE the tribe-score recompute, so a freshly recomputed score is held in
///     memory and only written on the next cycle (the legacy one-cycle lag). NOTE: Fenrir's tribe points live on
///     the normalised <c>game.WorldStateTribes</c> table (flushed by the world flush), not on a monolithic world
///     row, so the lag is preserved by ordering the recompute after the flush rather than by table layout.
/// </remarks>
public sealed class WorldStateFlushHost(
    IWorldStateAuthority worldState,
    IHeroRankAuthority heroRank,
    TowerStoreAuthority towerStore,
    ICenterTribeScoreSource tribeScoreSource,
    ICenterLinkBroadcaster broadcaster,
    ILogger<WorldStateFlushHost> logger) : BackgroundService
{
    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan FinalFlushBudget = TimeSpan.FromSeconds(15);

    private const short PendingFlag = 1;
    private const short ConsumedFlag = 2;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(FlushInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                await RunCycleAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await FinalFlushAsync().ConfigureAwait(false);
        }
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        try
        {
            await worldState.FlushIfDirtyAsync(ct).ConfigureAwait(false);
            await RecomputeTribeScoresAsync(ct).ConfigureAwait(false);
            await heroRank.FlushDirtyAsync(ct).ConfigureAwait(false);
            await towerStore.FlushDirtyAsync(ct).ConfigureAwait(false);
            await PollManualRecomputeAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Center world-state flush cycle failed -- next cycle retries");
        }
    }

    private async Task RecomputeTribeScoresAsync(CancellationToken ct)
    {
        try
        {
            var aggregates = await tribeScoreSource.ComputeAsync(ct).ConfigureAwait(false);
            var scores = TribeScoreRecompute.ToScores(aggregates);
            worldState.OverwriteTribePoints(scores);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Legacy swallowed this silently (S07_MyGame01.cpp:244); surfaced here. Previous scores are kept.
            logger.LogError(ex,
                "Center tribe-score recompute failed -- keeping previous tribe scores this cycle (Bug 3 source)");
        }
    }

    private async Task PollManualRecomputeAsync(CancellationToken ct)
    {
        if (!worldState.TryConsumeUpdateTribePointFlag(PendingFlag, ConsumedFlag))
            return;

        if (worldState.HighTribe is not { } favoredTribe)
        {
            logger.LogWarning(
                "Manual tribe-point recompute consumed but no advantaged tribe is recorded -- skipping, no 1234 broadcast");
            return;
        }

        var totals = FavoredTribeRankLadder.ComputeTotals(favoredTribe);
        worldState.OverwriteTribePoints(totals);

        await BroadcastTribePointSyncAsync(totals, ct).ConfigureAwait(false);

        logger.LogInformation(
            "Manual tribe-point recompute applied for advantaged tribe {FavoredTribe}: {T0}/{T1}/{T2}/{T3}",
            favoredTribe, totals[0], totals[1], totals[2], totals[3]);
    }

    private async Task BroadcastTribePointSyncAsync(int[] totals, CancellationToken ct)
    {
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
            logger.LogError(ex, "Tribe-point sync (1234) broadcast failed -- flag already consumed for this cycle");
        }
    }

    private async Task FinalFlushAsync()
    {
        using var cts = new CancellationTokenSource(FinalFlushBudget);
        try
        {
            logger.LogInformation("Center world-state final flush on stop");
            await worldState.FlushIfDirtyAsync(cts.Token).ConfigureAwait(false);
            await heroRank.FlushDirtyAsync(cts.Token).ConfigureAwait(false);
            await towerStore.FlushDirtyAsync(cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Center world-state final flush on stop failed or timed out");
        }
    }
}

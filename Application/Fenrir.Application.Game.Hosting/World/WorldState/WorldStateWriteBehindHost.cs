using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Application.Game.Hosting.World.WorldState;

/// <summary>
///     Periodic write-behind for <see cref="WorldStateService" />, mirroring the legacy ts25center tick
///     (S07_MyGame01.cpp:241-267: unconditional re-persist of mWorldInfo/mTribeInfo every 6 ticks, ~6s at
///     TimeLogic=1000). Same 5s cadence as <c>PositionWriteBehindHost</c>'s default, but skips the round trip
///     entirely when nothing changed (<see cref="WorldStateService.FlushIfDirtyAsync" /> no-ops on a clean cache).
///     <para>
///         Every cycle also runs <see cref="WorldStateService.ReconcileAsync" /> immediately after the flush,
///         in that exact order: flush-then-reread is what makes this shard's own just-flushed delta already
///         be summed into the DB total by the time this shard reads it back, and what lets every OTHER
///         shard's concurrent flush converge here within one interval -- reversing the order, or moving
///         reconcile to a separately-scheduled host, would lose that ordering guarantee.
///     </para>
///     <para>
///         <b>Process-kill residual risk, exact bound, and why this interval was NOT tightened:</b> unlike
///         <c>TowerWarWriteBehindHost</c>/<c>HeroRankPointsWriteBehindHost</c>/
///         <c>MonsterBossRespawnWriteBehindHost</c>/<c>ProxyShopExpiryFlushHost</c>/
///         <c>TribeBankTaxSweepFlushHost</c> (all shortened to 2s in this pass because their per-cycle cost is
///         a cheap in-memory dirty check with no DB round trip when clean),
///         <see cref="WorldStateService.ReconcileAsync" />
///         issues an UNCONDITIONAL <c>game.WorldState</c>/<c>WorldStateTribes</c>/<c>WorldStateAllianceOffers</c>
///         read every single cycle regardless of <see cref="WorldStateService.IsDirty" />, run by every live
///         shard on the same cadence -- tightening this interval would multiply that unconditional per-shard
///         read frequency, a real (if modest) DB load increase, not a free one. The interval therefore stays
///         at 5s. The bound on data lost to a true (non-graceful) process kill is: up to 5 seconds of
///         RvR world-state mutation (tribe symbols/points/gate/alliance-offer changes) on THIS shard that no
///         other shard has reconciled yet; points-deltas specifically are additive at the DB
///         (<see cref="Fenrir.Data.Abstractions.World.IWorldStateRepository.AddTribePointsAsync" />) so a lost
///         in-memory delta is a genuine loss of those points, never a double-count risk. This is an accepted,
///         bounded tradeoff, not a silent gap.
///     </para>
/// </summary>
public sealed class WorldStateWriteBehindHost(WorldStateService worldState) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await worldState.FlushIfDirtyAsync(stoppingToken).ConfigureAwait(false);
                await worldState.ReconcileAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown -- fall through to the final flush below.
        }

        // Best-effort final flush so a graceful shutdown doesn't lose the last few seconds of state.
        // FlushIfDirtyAsync itself already logs and swallows failures -- never let shutdown throw. No final
        // reconcile: the process is exiting, so there is no in-memory cache left for a fresh DB read to
        // usefully update.
        await worldState.FlushIfDirtyAsync(CancellationToken.None).ConfigureAwait(false);
    }
}

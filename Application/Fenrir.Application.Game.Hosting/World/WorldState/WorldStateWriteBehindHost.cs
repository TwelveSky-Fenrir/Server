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

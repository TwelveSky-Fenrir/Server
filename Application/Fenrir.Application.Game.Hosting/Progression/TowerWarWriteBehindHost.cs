using Fenrir.Application.Game.Domain.Progression;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Application.Game.Hosting.Progression;

/// <summary>
///     Periodic write-behind for <see cref="TowerWarState" />, same "skip the round trip when nothing changed"
///     shape as <see cref="World.WorldState.WorldStateWriteBehindHost" /> -- <see cref="TowerWarState.FlushDirtyAsync" />
///     no-ops on a cache with no dirty tower.
/// </summary>
/// <remarks>
///     <b>Process-kill residual risk, exact bound:</b> a true (non-graceful) process kill loses at most
///     <see cref="Interval" /> of dirty tower state (Level/TowerType/ControllingTribeId -- see
///     <see cref="TowerWarState" />'s own remarks for what does and doesn't persist). <see cref="Interval" />
///     was tightened from the original 5s to 2s in this pass specifically BECAUSE it was free to do so:
///     <see cref="TowerWarState.FlushDirtyAsync" />'s per-cycle cost with nothing dirty is an O(
///     <see cref="TowerWarState.TowerCount" />
///     = 12) array-of-bool scan, no DB round trip, and a tower only becomes dirty on a guardian
///     creation/level-change/death event -- rare compared to the polling cadence, unlike the continuously-dirty
///     Position/Progress cache (see <c>PositionWriteBehindHost</c>'s own remarks for why THAT one was not
///     tightened). This is an accepted, bounded tradeoff, not a silent gap.
/// </remarks>
public sealed class TowerWarWriteBehindHost(TowerWarState towerWar, ITowerRepository towers) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                await towerWar.FlushDirtyAsync(towers, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown -- fall through to the final flush below.
        }

        // Best-effort final flush so a graceful shutdown doesn't lose the last few seconds of state.
        // FlushDirtyAsync itself already logs and swallows failures -- never let shutdown throw.
        await towerWar.FlushDirtyAsync(towers, CancellationToken.None).ConfigureAwait(false);
    }
}

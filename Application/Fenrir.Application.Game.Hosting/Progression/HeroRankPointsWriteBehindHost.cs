using Fenrir.Application.Game.Domain.Progression;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Application.Game.Hosting.Progression;

/// <summary>
///     Periodic write-behind for <see cref="HeroRankPointAccumulator" />, same "skip the round trip when
///     nothing changed" shape as <see cref="TowerWarWriteBehindHost" />.
/// </summary>
/// <remarks>
///     <b>Process-kill residual risk, exact bound:</b> a true (non-graceful) process kill loses at most
///     <see cref="Interval" /> of accumulated-but-unflushed PvP-kill hero-rank point deltas per character (the
///     earning character's own live <see cref="Fenrir.Application.Game.Domain.World.PlayerRuntimeState.HeroRankPoints" />
///     total is unaffected -- only the DB-durable side is at risk, see <see cref="HeroRankPointAccumulator" />'s
///     own remarks). <see cref="Interval" /> was tightened from the original 5s to 2s in this pass because
///     <see cref="HeroRankPointAccumulator.FlushDirtyAsync" />'s per-cycle cost with nothing pending is a cheap
///     dictionary-emptiness check, no DB round trip, and grants are batched per-character (not per-kill), so a
///     shorter interval does not multiply round-trips per kill -- only reduces how many kills a lost row could
///     represent. This is an accepted, bounded tradeoff, not a silent gap.
/// </remarks>
public sealed class HeroRankPointsWriteBehindHost(
    HeroRankPointAccumulator heroRankPoints,
    IHeroRankingRepository heroRankings) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                await heroRankPoints.FlushDirtyAsync(heroRankings, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown -- fall through to the final flush below.
        }

        // Best-effort final flush so a graceful shutdown doesn't lose the last few seconds of pending grants.
        // FlushDirtyAsync itself already logs and swallows failures -- never let shutdown throw.
        await heroRankPoints.FlushDirtyAsync(heroRankings, CancellationToken.None).ConfigureAwait(false);
    }
}

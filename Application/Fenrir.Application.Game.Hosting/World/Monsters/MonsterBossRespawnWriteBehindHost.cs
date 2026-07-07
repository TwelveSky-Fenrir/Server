using Fenrir.Application.Game.Domain.World.Monsters;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Application.Game.Hosting.World.Monsters;

/// <summary>
///     Periodic write-behind for <see cref="MonsterBossRespawnTracker" />, same "skip the round trip when
///     nothing changed" shape as <c>WorldStateWriteBehindHost</c>/<c>TowerWarWriteBehindHost</c>.
/// </summary>
/// <remarks>
///     <b>Process-kill residual risk, exact bound:</b> a true (non-graceful) process kill loses at most
///     <see cref="Interval" /> of a just-recorded named-boss (monsters 564-568) death/respawn-deadline update
///     -- worst case, a boss killed just before the kill is lost respawns as if it hadn't been killed at all on
///     the next boot. <see cref="Interval" /> was tightened from the original 5s to 2s in this pass because
///     <see cref="MonsterBossRespawnTracker.FlushDirtyAsync" />'s per-cycle cost with nothing dirty is a cheap
///     <c>ConcurrentDictionary.IsEmpty</c> check, no DB round trip, and these named bosses die on the order of
///     hours apart, not seconds -- tightening the poll adds no meaningful load. This is an accepted, bounded
///     tradeoff, not a silent gap.
/// </remarks>
public sealed class MonsterBossRespawnWriteBehindHost(
    MonsterBossRespawnTracker tracker,
    IMonsterBossRespawnTimerRepository repository) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                await tracker.FlushDirtyAsync(repository, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown -- fall through to the final flush below.
        }

        // Best-effort final flush so a graceful shutdown doesn't lose the last few seconds of state.
        // FlushDirtyAsync itself already logs and swallows failures -- never let shutdown throw.
        await tracker.FlushDirtyAsync(repository, CancellationToken.None).ConfigureAwait(false);
    }
}

using Fenrir.Data.World;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Application.Game.World.Monsters;

/// <summary>
///     Periodic write-behind for <see cref="MonsterBossRespawnTracker" />, same "skip the round trip when
///     nothing changed" shape as <c>WorldStateWriteBehindHost</c>/<c>TowerWarWriteBehindHost</c>.
/// </summary>
public sealed class MonsterBossRespawnWriteBehindHost(
    MonsterBossRespawnTracker tracker,
    IMonsterBossRespawnTimerRepository repository) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

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

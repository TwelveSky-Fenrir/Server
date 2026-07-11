using Fenrir.Application.Game.Domain.World.Monsters;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Application.Game.Hosting.World.Monsters;

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
        }

        await tracker.FlushDirtyAsync(repository, CancellationToken.None).ConfigureAwait(false);
    }
}

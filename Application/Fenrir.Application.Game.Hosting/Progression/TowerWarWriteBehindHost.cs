using Fenrir.Application.Game.Domain.Progression;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Application.Game.Hosting.Progression;

/// <summary>
///     Periodic write-behind for <see cref="TowerWarState" />, same "skip the round trip when nothing changed"
///     shape as <see cref="World.WorldState.WorldStateWriteBehindHost" /> -- <see cref="TowerWarState.FlushDirtyAsync" />
///     no-ops on a cache with no dirty tower.
/// </summary>
public sealed class TowerWarWriteBehindHost(TowerWarState towerWar, ITowerRepository towers) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(5);

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

using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Application.Game.Hosting.World.WorldState;

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
        }

        var flushed = await worldState.FlushIfDirtyAsync(CancellationToken.None).ConfigureAwait(false);
        if (!flushed || worldState.IsDirty)
            throw new InvalidOperationException("WorldState shutdown flush left retained mutations unpersisted.");
    }
}

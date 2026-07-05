using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Hosting;

namespace Fenrir.Application.Game.Hosting.World.WorldState;

/// <summary>
///     Periodic write-behind for <see cref="WorldStateService" />, mirroring the legacy ts25center tick
///     (S07_MyGame01.cpp:241-267: unconditional re-persist of mWorldInfo/mTribeInfo every 6 ticks, ~6s at
///     TimeLogic=1000). Same 5s cadence as <c>PositionWriteBehindHost</c>'s default, but skips the round trip
///     entirely when nothing changed (<see cref="WorldStateService.FlushIfDirtyAsync" /> no-ops on a clean cache).
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
                await worldState.FlushIfDirtyAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown -- fall through to the final flush below.
        }

        // Best-effort final flush so a graceful shutdown doesn't lose the last few seconds of state.
        // FlushIfDirtyAsync itself already logs and swallows failures -- never let shutdown throw.
        await worldState.FlushIfDirtyAsync(CancellationToken.None).ConfigureAwait(false);
    }
}

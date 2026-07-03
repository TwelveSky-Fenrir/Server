using Fenrir.Application.Game.World;

namespace Fenrir.GameServer;

/// <summary>
///     Runs one tick loop per hosted zone for the process lifetime (ADR-0012, report 06 §6.2): each
///     <see cref="Zone" /> in the <see cref="ZoneRegistry" /> gets its own <see cref="Zone.RunAsync" /> task —
///     one <c>PeriodicTimer</c> per actor. At 20 Hz and a few dozen maps, a task per zone is trivial for the
///     runtime; a shared scheduler only becomes worth its complexity at hundreds of zones.
/// </summary>
public sealed class ZoneTickHost(ZoneRegistry zones) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Task.WhenAll (not sequential awaits): a crashed zone loop surfaces without silencing the others,
        // and cancellation stops them all through the shared token.
        return Task.WhenAll(zones.Zones.Select(zone => zone.RunAsync(stoppingToken)));
    }
}

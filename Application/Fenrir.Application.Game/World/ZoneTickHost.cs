using Microsoft.Extensions.Hosting;

namespace Fenrir.Application.Game.World;

/// <summary>
///     One <see cref="Zone.RunAsync" /> task per hosted zone. Trivial at a few dozen maps and 20 Hz; a shared
///     scheduler only pays off at hundreds of zones.
/// </summary>
public sealed class ZoneTickHost(ZoneRegistry zones) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // WhenAll, not sequential awaits: a crashed zone surfaces without silencing the others.
        return Task.WhenAll(zones.Zones.Select(zone => zone.RunAsync(stoppingToken)));
    }
}

using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World.WorldState;

/// <summary>
///     Drains every hosted zone's due tribe-bank-tax sweep (<see cref="Zone.DrainPendingTribeBankTaxSweeps" />,
///     queued by <see cref="Zone.Tick" /> once its own 10-zone-uptime-minute cadence is reached) and forwards
///     each one to <see cref="ITribeBankTaxSweepGateway" /> -- the write-behind twin of
///     <c>MonsterLootFlushHost</c> for this cluster's own periodic sweep, same "keep every zone's Tick fully
///     synchronous" rationale.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:2610-2620 (periodic 10-minute sweep, fire-and-forget send).
///     The 5 s poll interval here is a Fenrir-side implementation detail with no legacy equivalent to cite: the
///     zone-local accumulator's own reset already happened synchronously inside <see cref="Zone.Tick" /> the
///     instant the 10-minute mark was crossed (see <see cref="TribeBankTaxAccumulator.TrySweep" />) -- this host
///     only decides how quickly the already-detached payload is hand-carried on to the gateway, unlike the
///     legacy, which sent inline on the very same thread that reset the array.
/// </remarks>
public sealed class TribeBankTaxSweepFlushHost(
    ZoneRegistry zones,
    ITribeBankTaxSweepGateway gateway,
    ILogger<TribeBankTaxSweepFlushHost> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     One drain-and-forward pass over every hosted zone -- pure I/O, no timer, so tests can call it
    ///     directly instead of racing <see cref="ExecuteAsync" />'s own polling loop.
    /// </summary>
    public async Task FlushOnceAsync(CancellationToken ct)
    {
        foreach (var zone in zones.Zones)
        {
            var sweeps = zone.DrainPendingTribeBankTaxSweeps();
            foreach (var sweep in sweeps)
                try
                {
                    await gateway.SweepAsync(zone.MapId, sweep, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Fire-and-forget by contract -- a failed send permanently loses this window's entire tax
                    // for every tribe on this zone; it must never crash the host or block any other zone's sweep.
                    logger.LogError(ex, "Zone {MapId}: tribe bank tax sweep send failed -- window's tax lost",
                        zone.MapId);
                }
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        do
        {
            await FlushOnceAsync(stoppingToken).ConfigureAwait(false);
        } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }
}

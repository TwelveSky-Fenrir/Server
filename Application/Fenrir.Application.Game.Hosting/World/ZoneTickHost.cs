using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World;

/// <summary>
///     One <see cref="Zone.RunAsync" /> task per hosted zone. Trivial at a few dozen maps and 20 Hz; a shared
///     scheduler only pays off at hundreds of zones.
/// </summary>
/// <remarks>
///     Each zone's tick loop runs under its own independent supervisor (<see cref="SuperviseZoneAsync" />)
///     rather than a bare <see cref="Task.WhenAll" /> over the raw <see cref="Zone.RunAsync" /> tasks.
///     <see cref="Zone.Tick" /> and <see cref="Zone.RunAsync" /> already contain any fault to the single tick
///     that raised it (see the zone-tick-loop-fault-isolation behavior contract), but a bare
///     <see cref="Task.WhenAll" /> over the raw tasks would still fault its aggregate task -- and therefore
///     this hosted service's own <c>ExecuteAsync</c> task -- the instant any single zone's task ever faulted
///     for any other reason. Under the Generic Host's default
///     <c>BackgroundServiceExceptionBehavior.StopHost</c> (no override exists anywhere in this repo), that
///     would tear down the ENTIRE shard process over a single map's fault -- a materially larger blast radius
///     than legacy's one-process-per-zone isolation ever allows. Per-zone supervision keeps that blast radius
///     to the one faulting map, restoring the legacy property even though every zone here shares one process.
/// </remarks>
public sealed class ZoneTickHost(ZoneRegistry zones, ILogger<ZoneTickHost> logger) : BackgroundService
{
    private static readonly TimeSpan RestartBackoff = TimeSpan.FromSeconds(1);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // WhenAll over the SUPERVISED wrapper tasks, not the raw zone tasks: each wrapper below only ever
        // completes (it never rethrows), so this WhenAll can only ever complete when every zone's supervisor
        // does -- a crashed zone is contained and restarted by its own supervisor, without silencing,
        // faulting, or otherwise affecting any other zone's ticking or this host's own run state.
        return Task.WhenAll(zones.Zones.Select(zone => SuperviseZoneAsync(zone, stoppingToken)));
    }

    /// <summary>
    ///     Runs one zone's tick loop for the lifetime of the host, restarting it (after a short backoff) if it
    ///     ever exits with an unexpected fault instead of letting that fault propagate. This is a last-resort
    ///     backstop only -- <see cref="Zone.RunAsync" /> already catches and logs a fault from any tick stage
    ///     without ever letting itself throw for that reason -- but nothing here assumes that containment can
    ///     never have a gap: a zone whose loop exits abnormally anyway is restarted rather than left
    ///     permanently frozen, and the fault never reaches the sibling <see cref="Task.WhenAll" /> tasks
    ///     supervising every OTHER hosted zone.
    /// </summary>
    private async Task SuperviseZoneAsync(Zone zone, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await zone.RunAsync(stoppingToken).ConfigureAwait(false);
                return; // RunAsync only returns normally once stoppingToken is canceled.
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return; // Normal shutdown, not a zone fault.
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex,
                    "Zone {MapId} tick loop exited unexpectedly and is being restarted; every other zone's " +
                    "ticking is unaffected", zone.MapId);
            }

            try
            {
                await Task.Delay(RestartBackoff, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return; // Host is shutting down during the backoff -- nothing left to restart.
            }
        }
    }
}

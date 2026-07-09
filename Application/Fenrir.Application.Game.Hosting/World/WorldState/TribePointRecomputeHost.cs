using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Hosting.World.WorldState;

/// <summary>
///     ts25center-equivalent periodic driver for the two tribe-point recompute mechanisms
///     (<see cref="TribePointLevelRecomputeService" />, <see cref="FavoredTribeRankBonusLadderService" />): runs
///     once immediately at boot, then every <see cref="TickGate" />th tick of its own ~1-second clock
///     thereafter (matching the legacy ts25center's own <c>TimeLogic=1000</c> cadence and its
///     <c>(mTickCount % 6) == 0</c> gate) -- NOT the 500 ms zone tick
///     (<see cref="Fenrir.Application.Game.Domain.Simulation.SimulationClock.LegacyTick" />), which belongs to
///     a different legacy process entirely.
///     <para>
///         Runs on EVERY shard, unconditionally -- unlike
///         <see cref="Fenrir.Application.Game.Hosting.World.ZoneWar.HolyStoneWarCycleHost" /> (Zone038's one
///         designated shard) or
///         <see cref="Fenrir.Application.Game.Hosting.World.ZoneWar.TribeVoteElectionCalendarHost" />
///         (server-number-37's one designated shard), the legacy ts25center this reproduces was never itself a
///         per-map/per-shard process -- it had no natural "which shard owns this" answer to translate. Both
///         recompute mechanisms read/write only cluster-wide, DB-of-record state (the full character roster;
///         the shared <see cref="WorldStateService" /> fields, already reconciled cross-shard by
///         <see cref="WorldStateService.ReconcileAsync" />), so every shard redundantly computing the identical
///         deterministic result is self-consistent, not a new correctness risk -- a deliberate scheduling
///         decision documented here since the legacy source does not resolve it (see
///         <see cref="FavoredTribeRankBonusLadderService" />'s own remarks for the sibling conflict this does
///         NOT resolve: which of the two mechanisms wins when both are due in the same cycle).
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25center/S07_MyGame01.cpp:240-244 (level-based recompute call site, tick%6==0) ;
///     :253-266 (ladder check-and-trigger call site, same tick%6==0 gate, sequentially after) ;
///     Server/ts25center/S08_MyDB.cpp:17-54 (<c>GetWorldInfo</c>, boot-time call site at line 42) ;
///     Server/BuildEU33/ServerInfo.ini:95 (<c>TimeLogic=1000</c>), Server/Header/socket.h:7-9
///     (<c>SetTimeLogic</c>, decrements by 1 ms clamped to a minimum of 1) -- basis for the ~1-second tick
///     period.
/// </remarks>
public sealed class TribePointRecomputeHost(
    TribePointLevelRecomputeService levelRecompute,
    FavoredTribeRankBonusLadderService ladder,
    ILogger<TribePointRecomputeHost> logger) : BackgroundService
{
    /// <summary>Server/ts25center/S07_MyGame01.cpp:240-244/253-266 -- both mechanisms share this same gate.</summary>
    public const int TickGate = 6;

    /// <summary>
    ///     Server/BuildEU33/ServerInfo.ini:95 + Server/Header/socket.h:7-9 -- ts25center's own tick period, distinct from
    ///     the zone tick.
    /// </summary>
    public static readonly TimeSpan CenterTick = TimeSpan.FromSeconds(1);

    private int _tickCount;

    /// <summary>
    ///     Runs both mechanisms unconditionally, regardless of the tick-gate counter -- the boot-time call site
    ///     (Server/ts25center/S08_MyDB.cpp:17-54, <c>GetWorldInfo</c> line 42), which happens once before the
    ///     periodic gate ever starts counting.
    /// </summary>
    public Task RunBootRecomputeAsync(CancellationToken ct)
    {
        return RunOnceAsync(ct);
    }

    /// <summary>
    ///     One tick of the underlying ~1-second clock; only every <see cref="TickGate" />th call actually runs
    ///     the recompute pair. Exposed directly (not just through <see cref="ExecuteAsync" />) so tests can
    ///     drive the gate without a real timer.
    /// </summary>
    public async Task TickAsync(CancellationToken ct)
    {
        _tickCount++;
        if (_tickCount % TickGate != 0)
            return;

        await RunOnceAsync(ct).ConfigureAwait(false);
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        await levelRecompute.RecomputeAsync(ct).ConfigureAwait(false);
        await ladder.TickIfPendingAsync(ct).ConfigureAwait(false);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunBootRecomputeAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(CenterTick);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                try
                {
                    await TickAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A missed tick just delays this cycle's tribe-point recompute to the next ~1s tick --
                    // never worth crashing the GameServer.
                    logger.LogError(ex, "Tribe point recompute tick failed");
                }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }
}

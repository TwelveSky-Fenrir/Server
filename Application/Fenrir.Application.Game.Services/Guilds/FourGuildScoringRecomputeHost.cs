using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Guilds;

/// <summary>
///     C17 Part B2 periodic driver for <see cref="FourGuildScoringService.RecomputeAsync" />: runs once
///     immediately at boot, then every <see cref="RecomputeInterval" /> thereafter --
///     <see cref="FourGuildScoringService" />'s
///     own remarks name this cadence explicitly ("the legacy recomputes at boot and every ~10 seconds of its
///     own tick loop... the driving cadence is a hosted service's concern, this service only does one
///     recompute per call"); this host is that concern. Same "boot-then-<c>PeriodicTimer</c>,
///     log-and-continue on a missed tick, never crash the process over one bad cycle" shape as
///     <c>Hosting.World.WorldState.TribePointRecomputeHost</c>. Lives in
///     <c>Fenrir.Application.Game.Services</c> rather than <c>Fenrir.Application.Game.Hosting</c> for the same
///     "Hosting has no project reference to Services" reason documented on
///     <see cref="FourGuildKillPointRelayHost" />.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25extra/S08_MyDB.cpp:1109-1140 (<c>LoadFourGuild</c>, the top-three
///     positive-standings query itself, already the sole citation backing
///     <see cref="FourGuildScoringService.RecomputeAsync" />) ;
///     Server/ts25extra/S07_MyGame01.cpp:42 (boot-time call site), :98-101,153-155 (the ~10-second periodic
///     re-run this host's own <see cref="RecomputeInterval" /> reproduces) -- all three citations already
///     carried on <see cref="FourGuildScoringService" />'s own class remarks; not re-derived here.
/// </remarks>
public sealed class FourGuildScoringRecomputeHost(
    FourGuildScoringService scoring,
    ILogger<FourGuildScoringRecomputeHost> logger) : BackgroundService
{
    /// <summary>Server/ts25extra/S07_MyGame01.cpp:98-101,153-155 -- the legacy's own ~10-second re-run cadence.</summary>
    public static readonly TimeSpan RecomputeInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    ///     One recompute cycle, exposed directly (not just through <see cref="ExecuteAsync" />) so tests can
    ///     drive it without a real timer.
    /// </summary>
    public async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            await scoring.RecomputeAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A missed cycle just delays the leaderboard refresh to the next ~10s tick --
            // FourGuildScoringService.RecomputeAsync already leaves the previously-published standings in
            // place on a failed read (see its own remarks), so there is nothing further to recover here.
            logger.LogError(ex, "Four-guild leaderboard recompute tick failed");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(RecomputeInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }
}

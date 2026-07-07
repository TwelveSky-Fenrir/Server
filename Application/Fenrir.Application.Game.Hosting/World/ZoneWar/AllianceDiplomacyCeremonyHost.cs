using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

/// <summary>
///     Per-tick driver for <see cref="AllianceDiplomacyCeremony" />, armed only on whichever live shard
///     currently hosts <see cref="GameServerOptions.AllianceTribeMapId" /> with
///     <see cref="GameServerOptions.AllianceTribeEnabled" /> set -- every other shard's instance of this host
///     is permanently inert. Same "compute <c>IsArmed</c> once at construction, log and return if not armed"
///     shape as <see cref="HolyStoneWarCycleHost" />/<see cref="TribeSymbolBattleSchedulerHost" />/
///     <see cref="TribeVoteElectionCalendarHost" />. Unlike those three siblings, this host also owns the
///     per-tick post-occupant scan (<see cref="AlliancePostOccupantScanner" />) that resolves
///     <see cref="AllianceDiplomacyCeremony.Tick" />'s two candidate parameters -- that class's own remarks
///     explain why the scan lives here (Hosting) rather than inside the Domain state machine itself.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:578-622 (<c>mCheckAllienceServer</c> arm gate,
///     server-number-37) and :3764-4012 (<c>Process_Allience_Server</c> itself) -- Fenrir shards by map, so
///     "the shard hosting the designated map" is the natural translation, same as its siblings above.
/// </remarks>
public sealed class AllianceDiplomacyCeremonyHost(
    IOptions<GameServerOptions> options,
    ZoneRegistry zoneRegistry,
    AllianceDiplomacyCeremony ceremony,
    ILogger<AllianceDiplomacyCeremonyHost> logger) : BackgroundService
{
    private readonly AlliancePostSite _site = new(
        options.Value.AllianceTribeMapId,
        options.Value.AlliancePost0X, options.Value.AlliancePost0Z,
        options.Value.AlliancePost1X, options.Value.AlliancePost1Z,
        options.Value.AlliancePostRadius);

    public bool IsArmed { get; } =
        zoneRegistry.TryGet(options.Value.AllianceTribeMapId, out _) && options.Value.AllianceTribeEnabled;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsArmed)
        {
            logger.LogInformation(
                "AllianceDiplomacyCeremonyHost is inert on this shard (designated map {MapId} not hosted here, or AllianceTribeEnabled={Enabled})",
                options.Value.AllianceTribeMapId, options.Value.AllianceTribeEnabled);
            return;
        }

        using var timer = new PeriodicTimer(SimulationClock.LegacyTick);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                Tick();
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    /// <summary>
    ///     Scans both posts and advances the ceremony by exactly one tick -- pure enough to drive directly
    ///     from a test without a real timer, same convention as
    ///     <see cref="TribeVoteElectionCalendarHost.TickAsync" />. A safe no-op if this shard does not
    ///     currently host <see cref="AlliancePostSite.MapId" /> (never expected once <see cref="IsArmed" />
    ///     gates <see cref="ExecuteAsync" />, but kept a no-op rather than throwing for direct test calls).
    /// </summary>
    public void Tick()
    {
        if (!zoneRegistry.TryGet(_site.MapId, out var zone) || zone is null)
            return;

        var (postOne, postTwo) = AlliancePostOccupantScanner.Scan(zone, _site);
        var result = ceremony.Tick(postOne, postTwo, DateOnly.FromDateTime(DateTime.UtcNow));

        if (result.Notice == AllianceCeremonyNotice.None)
            return;

        // GAP 3 (AllianceDiplomacyCeremony's own remarks): no cited wire packet for these 5 status codes
        // yet -- logged only until fenrir-wire-protocol-implementer defines one.
        logger.LogInformation(
            "AllianceDiplomacyCeremony: {Notice} -- post one {RecipientOne}, post two {RecipientTwo}, remaining {Remaining}",
            result.Notice, result.RecipientOne, result.RecipientTwo, result.RemainingCountdown);
    }
}

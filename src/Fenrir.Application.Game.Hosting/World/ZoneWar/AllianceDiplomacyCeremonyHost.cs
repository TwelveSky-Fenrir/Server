using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

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
                try
                {
                    Tick();
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Alliance Diplomacy ceremony tick failed");
                }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Tick()
    {
        if (!zoneRegistry.TryGet(_site.MapId, out var zone) || zone is null)
            return;

        var (postOne, postTwo) = AlliancePostOccupantScanner.Scan(zone, _site);
        var result = ceremony.Tick(postOne, postTwo, DateOnly.FromDateTime(DateTime.Now));

        if (result.Notice == AllianceCeremonyNotice.None)
            return;

        var sort = AllianceCeremonyNoticeWireSort.Of(result.Notice);
        if (sort != 0)
        {
            var response = new TribeAllianceStatusResponse { Sort = sort, Value = result.RemainingCountdown };
            SendCeremonyNotice(zone, result.RecipientOne, response);
            SendCeremonyNotice(zone, result.RecipientTwo, response);
        }

        if (result.Notice is AllianceCeremonyNotice.NewAllianceProgress
            or AllianceCeremonyNotice.AlreadyAlliedProgress)
            return;

        logger.LogInformation(
            "AllianceDiplomacyCeremony: {Notice} -- post one {RecipientOne}, post two {RecipientTwo}, remaining {Remaining}",
            result.Notice, result.RecipientOne, result.RecipientTwo, result.RemainingCountdown);
    }

    private void SendCeremonyNotice(Zone zone, AllianceCeremonyCandidate? recipient,
        in TribeAllianceStatusResponse response)
    {
        if (recipient is not { } candidate)
            return;

        if (!zone.TryGetPlayer(candidate.CharacterId, out var state) || state is null || state.IsMovingZone)
            return;

        try
        {
            state.Session.Send(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Alliance ceremony notice to character {CharacterId} failed", candidate.CharacterId);
        }
    }
}

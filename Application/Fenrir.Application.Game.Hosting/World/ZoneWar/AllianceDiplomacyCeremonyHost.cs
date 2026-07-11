using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.ZoneWar;
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
        var result = ceremony.Tick(postOne, postTwo, DateOnly.FromDateTime(DateTime.UtcNow));

        if (result.Notice == AllianceCeremonyNotice.None)
            return;

        logger.LogInformation(
            "AllianceDiplomacyCeremony: {Notice} -- post one {RecipientOne}, post two {RecipientTwo}, remaining {Remaining}",
            result.Notice, result.RecipientOne, result.RecipientTwo, result.RemainingCountdown);
    }
}

using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class RegularWarAfkTickSystem(
    RegularWarActiveMapTracker regularWarTracker,
    IOptions<GameServerOptions> options,
    ILogger<RegularWarAfkTickSystem> logger) : ISimulationSystem
{
    public const int Zone195FullUnits = 10;

    public const int WarActiveFullUnits = 5;

    public const int UnitLegacyTicks = 60;

    public const int DisconnectGraceLegacyTicksPastFull = 2;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var opts = options.Value;
        var isZone195 = opts.Zone195MapIds.Contains(zone.MapId);

        if (!isZone195 && !RegularWarMapCatalog.TryGet(zone.MapId, out _))
            return;

        var isWarActive = !isZone195 && regularWarTracker.IsBattleInProgress(zone.MapId);

        if (!isZone195 && !isWarActive)
        {
            ResetEveryPlayer(zone);
            return;
        }

        var fullUnits = isZone195 ? Zone195FullUnits : WarActiveFullUnits;
        var fullTicks = fullUnits * UnitLegacyTicks;

        List<PlayerRuntimeState>? toAutoReturn = null;
        List<PlayerRuntimeState>? toDisconnect = null;

        foreach (var player in zone.Players)
        {
            if (player.IsMovingZone)
                continue;

            var previousTicks = player.AfkTick;
            player.AfkTick += legacyTicksElapsed;

            var previousUnit = previousTicks / UnitLegacyTicks;
            var currentUnit = player.AfkTick / UnitLegacyTicks;
            if (currentUnit > previousUnit && player.AfkTick < fullTicks)
                logger.LogInformation(
                    "RegularWarAfkTick: character {CharacterId} on zone {MapId} idle warning {Current}/{Full}",
                    player.CharacterId, zone.MapId, currentUnit, fullUnits);

            if (previousTicks < fullTicks && player.AfkTick >= fullTicks)
                (toAutoReturn ??= []).Add(player);

            if (player.AfkTick >= fullTicks + DisconnectGraceLegacyTicksPastFull)
                (toDisconnect ??= []).Add(player);
        }

        if (toAutoReturn is not null)
            foreach (var player in toAutoReturn)
                player.Session.Send(new ReturnToHomeZoneResponse());

        if (toDisconnect is null)
            return;

        foreach (var player in toDisconnect)
            if (player.Session is ClientSession client)
                client.Abort(DisconnectReason.IdleTimeout);
    }

    private static void ResetEveryPlayer(Zone zone)
    {
        foreach (var player in zone.Players)
            if (player.AfkTick != 0)
                player.AfkTick = 0;
    }
}

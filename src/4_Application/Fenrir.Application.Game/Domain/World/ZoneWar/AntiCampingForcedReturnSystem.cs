using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Packets.Zone;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public sealed class AntiCampingForcedReturnSystem(AntiCampingGuardPointCatalog catalog) : ISimulationSystem
{
    public const float ProximityRadius = 40.0f;

    public const int ForcedReturnThreshold = 20;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        if (!catalog.IsGuardedMap(zone.MapId))
            return;

        var points = catalog.GetPoints(zone.MapId);
        if (points.HolyStoneSymbolPoints.IsEmpty && points.TowerPoint is null)
            return;

        foreach (var player in zone.Players)
            EvaluatePlayer(player, points, legacyTicksElapsed);
    }

    private static void EvaluatePlayer(PlayerRuntimeState player, AntiCampingMapGuardPoints points,
        int legacyTicksElapsed)
    {
        var flaggedThisTick = false;

        foreach (var symbolPoint in points.HolyStoneSymbolPoints)
            flaggedThisTick |= EvaluatePoint(player, symbolPoint, legacyTicksElapsed);

        if (!flaggedThisTick && points.TowerPoint is { } towerPoint)
            EvaluatePoint(player, towerPoint, legacyTicksElapsed);

        if (player.AntiCampingProximityCounter >= ForcedReturnThreshold)
            player.Session.Send(new ReturnToHomeZoneResponse());
    }

    private static bool EvaluatePoint(PlayerRuntimeState player, AntiCampingGuardPoint point, int legacyTicksElapsed)
    {
        if (!IsWithinRadius(player, point))
        {
            player.AntiCampingProximityCounter = 0;
            return false;
        }

        player.AntiCampingProximityCounter += legacyTicksElapsed;
        return true;
    }

    private static bool IsWithinRadius(PlayerRuntimeState player, AntiCampingGuardPoint point)
    {
        var dx = player.PosX - point.X;
        var dy = player.PosY - point.Y;
        var dz = player.PosZ - point.Z;
        var distanceSquared = dx * dx + dy * dy + dz * dz;
        return distanceSquared <= ProximityRadius * ProximityRadius;
    }
}

using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Progression;

public sealed class TowerGuardianSystem(
    TowerWarState towerWar,
    WorldDataCache worldData,
    Lazy<ZoneEventBroadcaster>? zoneEventBroadcaster = null) : ISimulationSystem
{
    private const float GuardianLeashRadius = 300f;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var towerIndex = TowerZoneIndexTable.GetTowerIndex(zone.MapId);
        if (towerIndex < 0)
            return;

        var guardianIndex = TowerWarState.GuardianServerIndex(towerIndex);

        switch (towerWar.GetPhase(towerIndex))
        {
            case TowerSiegePhase.Building:
                TrySpawnGuardian(zone, towerIndex, guardianIndex);
                break;

            case TowerSiegePhase.Active:
                if (!zone.TryGetMonster(guardianIndex, out _))
                    towerWar.BeginSiege(towerIndex, DateTime.UtcNow);
                break;

            case TowerSiegePhase.Sieged:
                if (towerWar.IsDueForDestruction(towerIndex, DateTime.UtcNow))
                {
                    towerWar.CompleteDestruction(towerIndex);

                    zoneEventBroadcaster?.Value.AnnounceTowerStatus(towerWar);
                }

                break;

            case TowerSiegePhase.Dormant:
            default:
                break;
        }
    }

    private void TrySpawnGuardian(Zone zone, int towerIndex, int guardianIndex)
    {
        var pendingPacked = towerWar.GetPendingPackedStateForBuilding(towerIndex);
        var level = TowerWarState.DecodeLevel(pendingPacked);
        var towerType = TowerWarState.DecodeType(pendingPacked);

        var monsterId = TowerGuardianCatalog.ResolveMonsterId(level, towerType);
        if (monsterId == 0 || !worldData.MonstersById.TryGetValue(monsterId, out var definition))
            return;

        if (!TowerGuardianCatalog.TryGetGuardianLocation(zone.MapId, out var x, out var y, out var z))
            return;

        if (zone.TryGetMonster(guardianIndex, out _))
            zone.DespawnMonsterSilently(guardianIndex);

        var guardian = MonsterEntity.Create(guardianIndex, zone.NextMonsterUniqueNumber(), definition.Monster,
            guardianIndex, x, y, z, GuardianLeashRadius);
        zone.SpawnMonster(guardian);

        towerWar.CompleteUpgrade(towerIndex);

        zoneEventBroadcaster?.Value.AnnounceTowerStatus(towerWar);
    }
}

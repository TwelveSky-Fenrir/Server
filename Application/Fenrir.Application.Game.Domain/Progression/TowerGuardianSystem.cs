using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     Drives one tower's guardian-monster lifecycle for whichever single zone hosts it (
///     <see cref="TowerZoneIndexTable" />)
///     -- the Fenrir counterpart of the legacy per-zone-process <c>MyGame::ProcessForTower</c>
///     (S07_MyGame01.cpp:13659-13780),
///     minus the from-scratch construction states (101/102/103/1), which need the item-667 "construct tower" flow
///     this cluster does not implement (see <see cref="TowerWarState" />'s own remarks).
/// </summary>
/// <remarks>
///     Reads/writes <see cref="Fenrir.Application.Game.World.Zone.TryGetMonster" />/
///     <see cref="Fenrir.Application.Game.World.Zone.SpawnMonster" />,
///     both tick-owned -- safe here because <see cref="ISimulationSystem.Simulate" /> always runs on the zone's own
///     tick thread, the same invariant <see cref="Monsters.MonsterSpawnScheduler" /> relies on. Guardian death is
///     detected by simple absence from the zone's live pool rather than draining <c>Zone</c>'s shared dead-monster
///     queue (already single-consumer, drained by <see cref="Monsters.MonsterSpawnScheduler" /> in the same tick).
/// </remarks>
public sealed class TowerGuardianSystem(TowerWarState towerWar, WorldDataCache worldData) : ISimulationSystem
{
    /// <summary>
    ///     No legacy leash constant exists for tower guardians (S10_MySummon.cpp never sets one) -- generous
    ///     enough that <see cref="Monsters.MonsterAiSystem" />'s pursuit/return-to-spawn leash never clips a
    ///     defender chasing an attacker away from the tower itself.
    /// </summary>
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
                    towerWar.CompleteDestruction(towerIndex);
                break;

            case TowerSiegePhase.Dormant:
            default:
                break; // needs the item-use "construct tower" flow -- out of scope here
        }
    }

    /// <summary>
    ///     Legacy <c>UpgradeTower</c> (S07_MyGame01.cpp:13492-13532): free whichever guardian is already standing
    ///     (level up or a just-booted resume), then summon the one for the tower's pending level/type. Retries
    ///     next tick if the catalog lookup somehow misses -- never throws, this runs on the zone's own tick.
    /// </summary>
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
    }
}

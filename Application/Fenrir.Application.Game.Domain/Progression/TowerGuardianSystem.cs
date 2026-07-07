using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;
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
/// <remarks>
///     <paramref name="zoneEventBroadcaster" /> is <see cref="Lazy{T}" />, not a direct reference, for the exact
///     same reason <see cref="Monsters.MonsterSpawnScheduler" />'s own <c>zoneEventBroadcaster</c> parameter is:
///     <see cref="ZoneEventBroadcaster" /> itself depends on <see cref="World.ZoneRegistry" />, and this system is
///     one of the <see cref="ISimulationSystem" /> instances <see cref="World.ZoneRegistry" /> resolves at its own
///     construction time -- a direct reference here would be a same-container constructor cycle. Deferring the
///     lookup until first use (this tower's first completed upgrade or destruction) resolves it after every
///     singleton, including <see cref="World.ZoneRegistry" /> itself, is already constructed and cached. Drives
///     <see cref="ZoneEventBroadcaster.AnnounceTowerStatus" /> (legacy tSort=752) on upgrade-completion
///     (<see cref="TrySpawnGuardian" />) and, separately, on destroy-completion (<see cref="Simulate" />'s
///     <see cref="TowerSiegePhase.Sieged" /> branch) -- see that method's own remarks for the exact citations.
/// </remarks>
public sealed class TowerGuardianSystem(
    TowerWarState towerWar,
    WorldDataCache worldData,
    Lazy<ZoneEventBroadcaster>? zoneEventBroadcaster = null) : ISimulationSystem
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
                {
                    towerWar.CompleteDestruction(towerIndex);

                    // Legacy U_ZONE_BROADCAST_FOR_CENTER_SEND(752, ...) on destroy-completion
                    // (S07_MyGame01.cpp:13759) -- cluster-wide, not just this zone; see
                    // ZoneEventBroadcaster.AnnounceTowerStatus's own remarks for the full citation.
                    zoneEventBroadcaster?.Value.AnnounceTowerStatus(towerWar);
                }

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

        // Legacy U_ZONE_BROADCAST_FOR_CENTER_SEND(752, ...) on upgrade-completion (S07_MyGame01.cpp:13733) --
        // cluster-wide, not just this zone; see ZoneEventBroadcaster.AnnounceTowerStatus's own remarks for the
        // full citation. Only reached once per upgrade: the next tick's GetPhase no longer reports Building
        // once CompleteUpgrade above has cleared the pending state, so Simulate stops calling this method.
        zoneEventBroadcaster?.Value.AnnounceTowerStatus(towerWar);
    }
}

using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Domain.Game.GameData;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Progression;

public sealed class TowerLifecycleSystem(
    TowerWarState towerWar,
    WorldDataCache worldData,
    Lazy<ZoneEventBroadcaster>? zoneEventBroadcaster = null,
    ILogger<TowerLifecycleSystem>? logger = null) : ISimulationSystem
{
    private const int Level1LevelCode = 2;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var towerIndex = TowerZoneIndexTable.GetTowerIndex(zone.MapId);
        if (towerIndex < 0)
            return;

        var now = DateTime.UtcNow;

        AdvanceConstruction(zone, towerIndex, now);

        towerWar.TryResetIdleAttackState(towerIndex, now);
        towerWar.TryClearStaleEngagement(towerIndex, now);

        ApplyPendingGuardianHeal(zone, towerIndex);
    }

    private void ApplyPendingGuardianHeal(Zone zone, int towerIndex)
    {
        if (!towerWar.TryConsumeGuardianHeal(towerIndex))
            return;

        var guardianIndex = TowerWarState.GuardianServerIndex(towerIndex);
        if (!zone.TryGetMonster(guardianIndex, out var guardian) || guardian is null)
            return;

        guardian.Heal(guardian.MaxLife / 10);
        zone.BroadcastMonsterActionChange(guardian);
    }

    private void AdvanceConstruction(Zone zone, int towerIndex, DateTime now)
    {
        var constructKind = towerWar.GetPendingConstructKind(towerIndex);
        if (constructKind > 0)
        {
            TrySpawnConstructionGuardian(zone, towerIndex, constructKind, now);
            return;
        }

        if (towerWar.IsCreateCooldownElapsed(towerIndex, now))
            towerWar.CompleteConstructionCooldown(towerIndex);
    }

    private void TrySpawnConstructionGuardian(Zone zone, int towerIndex, int constructKind, DateTime now)
    {
        var monsterId = TowerGuardianCatalog.ResolveMonsterId(Level1LevelCode, constructKind);
        if (monsterId == 0 || !worldData.MonstersById.TryGetValue(monsterId, out var definition))
            return;

        if (!TowerGuardianCatalog.TryGetGuardianLocation(zone.MapId, out var x, out var y, out var z))
            return;

        var guardianIndex = TowerWarState.GuardianServerIndex(towerIndex);
        if (zone.TryGetMonster(guardianIndex, out _))
            zone.DespawnMonsterSilently(guardianIndex);

        var guardian = MonsterEntity.Create(guardianIndex, zone.NextMonsterUniqueNumber(), definition.Monster,
            guardianIndex, x, y, z);
        zone.SpawnMonster(guardian);

        towerWar.CompleteConstructionSpawn(towerIndex, now);

        zoneEventBroadcaster?.Value.AnnounceTowerStatus(towerWar);
        logger?.LogInformation(
            "Tower {TowerIndex} constructed (Center broadcast 752/753): kind={Kind} on map {MapId}, attack-state reset to ready; post-create cooldown started",
            towerIndex, constructKind, zone.MapId);
    }
}

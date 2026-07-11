using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.Monsters;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     A11 -- the from-scratch tower-construction lifecycle (item 665) plus the tower-attack AI's idle timers,
///     for whichever single zone hosts each tower (<see cref="TowerZoneIndexTable" />). The Fenrir counterpart of
///     the legacy per-zone <c>MyGame::ProcessForTower</c> creating/cooldown branches (S07_MyGame01.cpp:13659-13722)
///     and the special-sort-10 guardian AI tick (S07_MyGame05.cpp:838-863) that
///     <see cref="Progression.TowerGuardianSystem" /> deliberately left out (it owns only the upgrade Building
///     and the Active/Sieged/destroy branches).
///     <para>
///         Construction is driven off <see cref="TowerWarState" />'s dedicated construct-kind/create-cooldown
///         fields, NOT the upgrade path's <c>_pendingPackedState</c>/<c>_valid</c>, so a tower mid-construction
///         reads as <see cref="TowerSiegePhase.Dormant" /> to <see cref="Progression.TowerGuardianSystem" />
///         (whose Dormant branch is a no-op) and the two systems drive strictly disjoint states -- no edit to
///         <see cref="Progression.TowerGuardianSystem" /> is required, and there is never a double-spawn. Once
///         construction completes the slot becomes <see cref="TowerSiegePhase.Active" /> and the existing system
///         takes over its siege/destroy handling.
///     </para>
///     <para>
///         Runs on the zone's own tick (single-writer invariant) -- <see cref="Zone.SpawnMonster" />/
///         <see cref="Zone.DespawnMonsterSilently" /> are tick-owned, same posture as
///         <see cref="Progression.TowerGuardianSystem" /> and <see cref="Monsters.MonsterSpawnScheduler" />.
///         <paramref name="zoneEventBroadcaster" /> is <see cref="Lazy{T}" /> for the identical DI-cycle reason
///         those two use it (see their remarks): it depends on <see cref="World.ZoneRegistry" />, which resolves
///         this very system at its own construction time.
///     </para>
/// </summary>
public sealed class TowerLifecycleSystem(
    TowerWarState towerWar,
    WorldDataCache worldData,
    Lazy<ZoneEventBroadcaster>? zoneEventBroadcaster = null,
    ILogger<TowerLifecycleSystem>? logger = null) : ISimulationSystem
{
    /// <summary>
    ///     Level-1 created-tower level code: DecodeLevel(200 + kind) == 2, so the level-1 guardian resolves through
    ///     the same <see cref="TowerGuardianCatalog" /> level table (2 -&gt; guardian block base) the upgrade path
    ///     already uses.
    /// </summary>
    private const int Level1LevelCode = 2;

    /// <summary>Same generous leash as <see cref="TowerGuardianSystem" />'s guardian -- no legacy leash constant exists.</summary>
    private const float GuardianLeashRadius = 300f;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var towerIndex = TowerZoneIndexTable.GetTowerIndex(zone.MapId);
        if (towerIndex < 0)
            return;

        var now = DateTime.UtcNow;

        AdvanceConstruction(zone, towerIndex, now);

        // Tower-attack AI idle timers (special sort 10, S07_MyGame05.cpp:844-863): wall-clock derived, not tick
        // counted, so they run every tick and self-gate on the elapsed span. Both are no-ops until a hit has
        // actually landed / an engagement's first hit has been recorded.
        towerWar.TryResetIdleAttackState(towerIndex, now);
        towerWar.TryClearStaleEngagement(towerIndex, now);

        ApplyPendingGuardianHeal(zone, towerIndex);
    }

    /// <summary>
    ///     A11 -- the tick-owned half of the item-667 guardian-heal handoff (contract side effect B,
    ///     S04_MyWork03.cpp:7506-7565): drains a heal armed by <see cref="TowerWarState.RequestGuardianHeal" />
    ///     (item already consumed on the handler thread -- see <c>TowerUpgradeService.HealAsync</c>) and applies
    ///     it to the live guardian as +10% of its own max life via <see cref="MonsterEntity.Heal" />, then
    ///     re-broadcasts the guardian so every nearby client's HP bar reflects the heal immediately, matching
    ///     every other FSM-transition broadcast in this file's family (<see cref="Zone.BroadcastMonsterActionChange" />).
    ///     Not <c>TowerRepairInfoResponse</c>/op153 -- that packet is never sent from any traced legacy call site
    ///     (dead code), so the existing monster-life-update broadcast path is the only live one.
    ///     <para>
    ///         A no-op, but still drains the pending flag, if the guardian is no longer alive by the time this
    ///         tick runs (e.g. it was destroyed between the item-use and this tick) -- a stale request must never
    ///         linger and silently apply to a future, unrelated guardian instance.
    ///     </para>
    /// </summary>
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

        // Post-create cooldown (legacy state 1 -> 2): once elapsed, the level-1 tower goes live/attackable.
        if (towerWar.IsCreateCooldownElapsed(towerIndex, now))
            towerWar.CompleteConstructionCooldown(towerIndex);
    }

    /// <summary>
    ///     Legacy creating-state pass (S07_MyGame01.cpp:13465-13489): free any standing guardian, summon the
    ///     level-1 guardian for the constructed kind, record the slot as level-1, start the post-create cooldown,
    ///     and announce the new state to the hub (event 752 tower state, 753 attack state). Retries next tick if
    ///     the catalog/location lookup misses -- never throws, this runs on the zone's own tick; the item was
    ///     already consumed when the construction was armed, matching the legacy's own non-transactional quirk
    ///     (contract edge case "monster summon fails during creation").
    /// </summary>
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
            guardianIndex, x, y, z, GuardianLeashRadius);
        zone.SpawnMonster(guardian);

        towerWar.CompleteConstructionSpawn(towerIndex, now);

        // Legacy U_ZONE_BROADCAST_FOR_CENTER_SEND(752, tower state) + (753, attack state) on create-completion
        // (S07_MyGame01.cpp:13704-13712). 752 is the real client-facing tower-state fan-out; 753's attack-state
        // hop has no receiving process in Fenrir's two-executable topology (same collapse the 754 first-hit hop
        // already documents in Zone.Combat.ApplyTowerGuardianHitSideEffects), so it is logged, not sent.
        zoneEventBroadcaster?.Value.AnnounceTowerStatus(towerWar);
        logger?.LogInformation(
            "Tower {TowerIndex} constructed (Center broadcast 752/753): kind={Kind} on map {MapId}, attack-state reset to ready; post-create cooldown started",
            towerIndex, constructKind, zone.MapId);
    }
}

using System.Collections.Concurrent;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.Quests;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World.Loot;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.Monsters;

/// <summary>
///     One pool slot for the Nth copy of one <c>world.MonsterSpawnRegions</c> row. <see cref="ServerIndex" />
///     is assigned once and stays stable for the zone's whole lifetime -- Fenrir has no fixed-size
///     shared-memory pool to recycle slots out of, unlike the legacy's <c>shmMONSTER_OBJECT[MAX_MONSTER_OBJECT_NUM]</c>.
/// </summary>
internal sealed class MonsterSpawnSlot
{
    public required MonsterSpawnRegionRowDto Region { get; init; }
    public required MonsterRowDto Monster { get; init; }
    public required int ServerIndex { get; init; }
    public bool Alive { get; set; }
    public int RespawnTicksRemaining { get; set; }
}

/// <summary>Everything this scheduler needs to remember for one zone.</summary>
internal sealed class MonsterZoneSpawnState
{
    public required List<MonsterSpawnSlot> Slots { get; init; }
    public required MonsterDropRoller DropRoller { get; init; }
    public required Random Random { get; init; }
    public bool InitialPopDone { get; set; }
    public int TicksSinceLastScan { get; set; }
}

/// <summary>
///     Owns every zone's monster spawn-region pool: initial pop, the ~10 s respawn scan, and the fallout of a
///     kill (loot roll + ground-item spawn + <see cref="Zone.GrantMonsterKillExperience" /> + arming that
///     slot's own respawn timer).
/// </summary>
/// <remarks>
///     A single DI singleton shared across every <see cref="Zone" />, so the actual per-zone mutable state
///     lives in a <see cref="MonsterZoneSpawnState" /> keyed by <see cref="Zone.MapId" /> in
///     <see cref="_stateByZone" />, built lazily on that zone's own first <see cref="Simulate" /> call (never
///     racy: each zone's tick is its own single thread).
///     <para>
///         Covers the generic per-region "normal monster" population, including two named-monster carve-outs
///         (<see cref="RollRespawnTicks" />'s monster-746 fixed cooldown, and <see cref="MonsterBossRespawnTracker" />'s
///         disk-persisted deadline for the 5 monsters 564-568) and the tribe/monster-symbol "Holy Stone" report
///         on kill (<see cref="ProcessDeath" />). Not ported: <c>SummonBossMonster</c> (a separate boss-table
///         state machine, <c>Server/ts25zone/S10_MySummon.cpp:1066-1216</c>) and <c>SummonGuard</c>/
///         <c>SummonTribeSymbol</c>
///         (hardcoded per-tribe-territory coordinates that depend on an unmodeled territory-ownership system) --
///         both summon their monsters outside <c>world.MonsterSpawnRegions</c> entirely, so neither is reachable
///         from this scheduler's own per-region pool. The dungeon <c>mNumber</c>-forced-to-20 instance-population
///         override (<c>Server/ts25zone/S10_MySummon.cpp:561-573</c>: dungeon zones requesting 5-19 copies of a
///         monster with ID &lt; 500 get bumped to 20) IS now ported as <see cref="DungeonSpawnDensityPolicy" />,
///         wired here via <see cref="BuildState" />'s <c>isDungeonZone</c> parameter
///         (<see cref="Zone.IsDungeonServerZone" />/<see cref="GameServerOptions.DungeonServerMapIds" />) --
///         empty by default, so inert until an operator lists a map id there. Since boss-sourced rows
///         (<c>Z0NN_SUMMONBOSSMONSTER.WREGION</c>) still ride in this same per-region pool rather than a
///         separate boss table, the boss-file-specific load-time adjustments legacy applies before
///         <c>SummonBossMonster</c> ever runs -- silently dropping the last row of an odd-count boss file, and
///         stamping a shared "last summon" timestamp only when the boss file yielded at least one row
///         (<c>Server/ts25zone/S10_MySummon.cpp:485-492,600-610</c>) -- are deferred alongside
///         <c>SummonBossMonster</c> itself, since neither has an observable effect without that state machine's
///         own port. <see cref="RegularMonsterTableCapacity" />'s overflow discard, below, is the one WREGION
///         load-time side effect from that same range this scheduler does reproduce today.
///     </para>
///     <para>
///         <paramref name="zoneEventBroadcaster" /> is <see cref="Lazy{T}" />, not a direct reference, because
///         <see cref="ZoneEventBroadcaster" /> itself depends on <see cref="ZoneRegistry" /> and this scheduler
///         is one of the <see cref="ISimulationSystem" /> instances <see cref="ZoneRegistry" /> resolves at
///         construction time -- a direct reference here would be a same-container constructor cycle
///         (ZoneRegistry -&gt; ISimulationSystem -&gt; MonsterSpawnScheduler -&gt; ZoneEventBroadcaster -&gt;
///         ZoneRegistry). Deferring the lookup until first use (i.e. the first monster kill) resolves it after
///         every singleton, including <see cref="ZoneRegistry" /> itself, is already constructed and cached.
///     </para>
///     <para>
///         <paramref name="valleyWarKillRegistry" /> needs no such deferral: <see cref="ZoneWar.ValleyWarKillRegistry" />
///         is a plain leaf singleton with no <see cref="ZoneRegistry" /> dependency of its own, so a direct
///         reference here carries no constructor-cycle risk. See <see cref="ProcessDeath" />'s own call site for
///         the Valley of the Deceased (Zone 200/297/298/299) kill-race quota decrement this feeds.
///     </para>
/// </remarks>
public sealed class MonsterSpawnScheduler(
    WorldDataCache worldData,
    Func<Random>? randomFactory = null,
    PartyRegistry? partyRegistry = null,
    Lazy<ZoneEventBroadcaster>? zoneEventBroadcaster = null,
    MonsterBossRespawnTracker? bossRespawnTracker = null,
    TowerWarState? towerWar = null,
    ValleyWarKillRegistry? valleyWarKillRegistry = null,
    BossDropCatalog? bossDropCatalog = null)
    : ISimulationSystem
{
    /// <summary>
    ///     Legacy's <c>END_NORMAL_MONSTER_OBJECT_NUM</c> (<c>Server/ts25zone/S01_MainApplication.cpp:38-57</c>,
    ///     per <c>ServerDocs/12_ts25zone/21_MyWorld_MySummon_Navmesh_Spawn.md</c> &#167;3.3): the running total of
    ///     every <c>world.MonsterSpawnRegions</c> row's slot count that <c>LoadRegionInfo_1</c> checks against
    ///     before committing a zone's spawn table, discarding the WHOLE table back to empty rather than
    ///     truncating it the moment the total crosses this ceiling (<c>Server/ts25zone/S10_MySummon.cpp:575-580</c>).
    ///     Legacy additionally resizes this ceiling per physical server number at boot -- doubled for the five
    ///     "_FIX" dungeon variants (39/144/145/313/74), collapsed to 1000 for instance-zone shards (241-330), to
    ///     1 for FFA maps (<c>Server/ts25zone/S02_MyServer.cpp:140-227</c>). The dungeon-variant doubling half of
    ///     that resizing IS now modeled, via <see cref="DungeonSpawnDensityPolicy.ResolveTableCapacity" />
    ///     (<see cref="BuildState" />'s <c>isDungeonZone</c> parameter) -- this constant remains the un-doubled
    ///     base value passed into that resolver. The instance-zone/FFA collapses are still not modeled (no
    ///     equivalent per-shard object-pool resizing concept exists here for those two cases). Applied to a
    ///     zone's whole combined spawn-region pool rather than a "regular-monster-only"
    ///     subset, because <see cref="BuildState" /> -- like the rest of this scheduler, see its own class
    ///     remarks -- does not separate boss-sourced rows (<c>Z0NN_SUMMONBOSSMONSTER.WREGION</c>) into a
    ///     distinct table the way legacy's <c>LoadRegionInfo_2</c> does; every live zone's seeded row total sits
    ///     nowhere near this ceiling, so the discard branch below is expected to be dead in steady state, same
    ///     as legacy's own experience of the check.
    /// </summary>
    private const int RegularMonsterTableCapacity = 3400;

    /// <summary>
    ///     The boss/event drop item-id data (<see cref="BossEventDropResolver" />'s DATA half). Defaults to the
    ///     process-wide <see cref="BossDropCatalog.Default" /> when DI/tests don't supply one -- it is an immutable
    ///     static asset, so a shared single instance across every zone is correct.
    /// </summary>
    private readonly BossDropCatalog _bossDropCatalog = bossDropCatalog ?? BossDropCatalog.Default;

    /// <summary>
    ///     A fresh <see cref="System.Random" /> per zone -- never shared across zones, since different zones
    ///     tick concurrently and <see cref="System.Random" /> is not thread-safe. Tests may inject a factory
    ///     returning a seeded <see cref="Random" /> for deterministic rolls.
    /// </summary>
    private readonly Func<Random> _randomFactory = randomFactory ?? (static () => new Random());

    private readonly ConcurrentDictionary<short, MonsterZoneSpawnState> _stateByZone = new();

    /// <summary>
    ///     "Demon Lord" (<see cref="BossEventDropResolver.DemonLordMonsterId" />) process-wide kill tally --
    ///     legacy's own function-local static counter (<c>Server/ts25zone/S07_MyGame05.cpp:2356-2394</c>), shared
    ///     across every zone/instance of this monster and every killer server-process-wide, never reset except by
    ///     a process restart. This scheduler is itself the one process-wide DI singleton every zone shares (see
    ///     class remarks), so a plain instance field here has the exact same lifetime/sharing semantics as the
    ///     legacy static -- incremented via <see cref="Interlocked.Increment(ref int)" /> since different zones'
    ///     kills of this monster can race each other on separate tick threads.
    /// </summary>
    private int _demonLordKillTally;

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var state = _stateByZone.GetOrAdd(zone.MapId, _ => BuildState(zone.MapId, zone.IsDungeonServerZone));

        if (!state.InitialPopDone)
        {
            // Every configured slot pops on the very first tick, unconditionally, before any respawn timer
            // logic applies -- except a slot BuildState pre-armed from a persisted deadline (the 5 named
            // "Yanggok" bosses, 564-568), which must wait that out like any other still-cooling-down slot.
            state.InitialPopDone = true;
            foreach (var slot in state.Slots)
                if (slot.RespawnTicksRemaining <= 0)
                    Spawn(zone, slot);
        }

        DrainDeaths(zone, state);

        foreach (var slot in state.Slots)
            if (!slot.Alive)
                slot.RespawnTicksRemaining = Math.Max(0, slot.RespawnTicksRemaining - legacyTicksElapsed);

        state.TicksSinceLastScan += legacyTicksElapsed;
        if (state.TicksSinceLastScan < SimulationClock.MonsterRespawnScanLegacyTicks)
            return;

        state.TicksSinceLastScan = 0;
        foreach (var slot in state.Slots)
            if (slot is { Alive: false, RespawnTicksRemaining: <= 0 })
                Spawn(zone, slot);
    }

    public int SlotCountFor(short mapId)
    {
        return _stateByZone.TryGetValue(mapId, out var state) ? state.Slots.Count : 0;
    }

    private MonsterZoneSpawnState BuildState(short mapId, bool isDungeonZone)
    {
        var regions = worldData.ZonesByNumber.TryGetValue(mapId, out var zoneDef)
            ? zoneDef.MonsterSpawnRegions
            : [];

        var resolved = new List<(MonsterSpawnRegionRowDto Region, MonsterRowDto Monster, int SpawnCount)>();
        var totalRequested = 0;
        foreach (var region in regions)
        {
            if (region.MonsterId is not { } monsterId ||
                !worldData.MonstersById.TryGetValue(monsterId, out var monsterDefinition))
                continue; // the cache is already filtered of these (WorldDataFilterStats) -- defensive only

            var spawnCount = DungeonSpawnDensityPolicy.ResolveConfiguredSpawnCount(isDungeonZone,
                Math.Max(0, region.Number), monsterDefinition.Monster.MonsterId);
            resolved.Add((region, monsterDefinition.Monster, spawnCount));
            totalRequested += spawnCount;
        }

        var slots = new List<MonsterSpawnSlot>();
        var nextServerIndex = 1;
        var now = DateTime.UtcNow;

        // RegularMonsterTableCapacity overflow discards the whole zone's table rather than truncating it --
        // see that constant's own remarks. Doubled when this shard is dungeon-flagged, matching legacy's own
        // per-process object-pool resizing (DungeonSpawnDensityPolicy.ResolveTableCapacity).
        var capacity = DungeonSpawnDensityPolicy.ResolveTableCapacity(isDungeonZone, RegularMonsterTableCapacity);
        if (totalRequested <= capacity)
            foreach (var (region, monster, slotCount) in resolved)
            {
                for (var i = 0; i < slotCount; i++)
                {
                    var slot = new MonsterSpawnSlot
                    {
                        Region = region,
                        Monster = monster,
                        ServerIndex = nextServerIndex++
                    };

                    // A restart must resume a persisted boss's cooldown instead of popping it back in at tick 1 --
                    // see MonsterBossRespawnTracker's own remarks.
                    if (IsPersistedBossMonster(monster.MonsterId) && bossRespawnTracker is { } tracker &&
                        tracker.TryGetNextSpawnUtc(region.MonsterSpawnRegionId, out var dueAtUtc))
                        slot.RespawnTicksRemaining = SimulationClock.ToWholeLegacyTicks(dueAtUtc - now);

                    slots.Add(slot);
                }
            }

        var random = _randomFactory();
        return new MonsterZoneSpawnState
        {
            Slots = slots,
            DropRoller = new MonsterDropRoller(worldData, random),
            Random = random
        };
    }

    /// <summary>
    ///     Random point inside the region's disk. When <see cref="Zone.Geometry" /> is loaded, the point must
    ///     resolve to a ground height or the whole attempt fails closed -- legacy's own <c>CreateSummon</c>
    ///     aborts identically on an off-mesh scatter point (<c>if ( !mWORLD.GetYCoord(...) ) { return FALSE; }</c>,
    ///     <c>Server/ts25zone/S10_MySummon.cpp:745-748</c>) and leaves the slot's cooldown state untouched so the
    ///     next periodic respawn scan re-rolls a fresh random point and retries -- see <see cref="Simulate" />'s
    ///     own callers, which never advance a slot's timer past zero on a failed attempt. A region whose exact
    ///     center is off-mesh (zero/absent radius) fails the same check, matching
    ///     <c>Server/ts25zone/S10_MySummon.cpp:724-763</c>'s <c>mRADIUS &gt; 0</c> gate only ever controlling
    ///     whether a nonzero scatter distance is drawn, never whether the ground-height check itself runs.
    ///     <para>
    ///         When <see cref="Zone.Geometry" /> is null (no <c>.WM</c> loaded for this zone at all), there is no
    ///         legacy equivalent to compare against -- production ts25zone always has terrain data for every
    ///         live map -- so this falls back to the region's raw recorded Y unconditionally rather than failing
    ///         closed, to avoid permanently starving spawns in a zone whose terrain file hasn't been supplied
    ///         (e.g. in tests). This is a Fenrir-only divergence, not a ported legacy behavior.
    ///     </para>
    /// </summary>
    private void Spawn(Zone zone, MonsterSpawnSlot slot)
    {
        var state = _stateByZone[zone.MapId];
        var region = slot.Region;
        var angle = state.Random.NextDouble() * (Math.PI * 2);
        var scatter = (float)(state.Random.NextDouble() * region.Radius);
        var x = region.LocationX + (float)(Math.Cos(angle) * scatter);
        var z = region.LocationZ + (float)(Math.Sin(angle) * scatter);
        var y = (float)region.LocationY;

        if (zone.Geometry is { } geometry)
        {
            if (!geometry.TryGetGroundHeight(x, z, out var groundY))
                return; // off-mesh scatter point: fail closed, retry next scan with a freshly drawn point
            y = groundY;
        }

        var leash = MathF.Max(region.Radius, 1f);
        var entity = MonsterEntity.Create(slot.ServerIndex, zone.NextMonsterUniqueNumber(), slot.Monster,
            slot.ServerIndex, x, y, z, leash);

        zone.SpawnMonster(entity);
        slot.Alive = true;
    }

    private void DrainDeaths(Zone zone, MonsterZoneSpawnState state)
    {
        while (zone.TryDequeueDeadMonster(out var death))
        {
            // Zone.TryDamageMonster already removed the dying monster from Zone's own _monsters dictionary
            // (safe from any thread) but deliberately left its monster-side AOI grid entry alone -- that grid
            // is tick-owned only, so the matching removal happens here instead, on this zone's own tick thread.
            zone.RemoveMonsterFromGrid(death!.Monster);

            var slot = state.Slots.Find(s => s.ServerIndex == death!.Monster.ServerIndex);
            if (slot is not null)
            {
                slot.Alive = false;
                var respawnTicks = RollRespawnTicks(slot.Monster, state.Random);
                slot.RespawnTicksRemaining = respawnTicks;

                if (IsPersistedBossMonster(slot.Monster.MonsterId) && bossRespawnTracker is { } tracker)
                    tracker.SetNextSpawnUtc(slot.Region.MonsterSpawnRegionId,
                        DateTime.UtcNow + SimulationClock.ToTimeSpan(respawnTicks));
            }

            ProcessDeath(zone, state, death!);
        }
    }

    /// <summary><c>mSummonTime[0..1]</c> is in seconds (<c>S10_MySummon.cpp:1845</c>) -- converted to legacy ticks here.</summary>
    private static int RollRespawnTicks(MonsterRowDto monster, Random random)
    {
        // Monster 746 ("Virgin Ghost"): legacy permanently overrides its respawn to a fixed 240s the moment it
        // has spawned once, superseding whatever SummonTime1/2 the catalog says from then on
        // (S10_MySummon.cpp:1043-1046). Applying it unconditionally is the steady-state-equivalent
        // simplification -- the only divergence is this monster's very first death on a freshly booted server,
        // where legacy would still roll the catalog window once before the override first kicks in.
        if (monster.MonsterId == 746)
            return SimulationClock.ToWholeLegacyTicks(TimeSpan.FromSeconds(240));

        var minSeconds = monster.SummonTime1;
        var maxSeconds = monster.SummonTime2;
        var seconds = maxSeconds > minSeconds ? minSeconds + random.Next(maxSeconds - minSeconds + 1) : minSeconds;
        return SimulationClock.ToWholeLegacyTicks(TimeSpan.FromSeconds(Math.Max(0, seconds)));
    }

    /// <summary>
    ///     YangGok old normal boss spawn IDs (legacy <c>YG_IsNormalBossTimerTarget</c>, <c>S10_MySummon.cpp:11-15</c>)
    ///     -- the only monsters whose respawn deadline is persisted via <see cref="MonsterBossRespawnTracker" /> so
    ///     it survives a GameServer restart instead of resetting to "ready now" every boot.
    /// </summary>
    private static bool IsPersistedBossMonster(int monsterId)
    {
        return monsterId is >= 564 and <= 568;
    }

    /// <summary>
    ///     Loot pipeline + XP-grant seam for one kill. Runs entirely on the zone's own tick thread; money
    ///     grants go to <see cref="Zone.QueueMoneyGrant" /> rather than being awaited here, since
    ///     <see cref="Zone.Tick" /> is fully synchronous and must never block on SQL I/O.
    /// </summary>
    private void ProcessDeath(Zone zone, MonsterZoneSpawnState state, DeadMonsterEvent death)
    {
        var monster = death.Monster;
        if (!worldData.MonstersById.TryGetValue(monster.Template.MonsterId, out var monsterDefinition))
            return; // cannot happen (the slot was built from this exact lookup), defensive only

        zone.BroadcastMonsterDeath(monster);

        PlayerRuntimeState? killer = null;
        if (death.KillerCharacterId is { } killerId)
            zone.TryGetPlayer(killerId, out killer);

        if (killer is not null && TribeSymbolIndexOf(monster.Template.SpecialType) is { } symbolIndex)
            // Legacy tallies cumulative per-tribe damage across the whole fight (mTribeDamageForTribeSymbol[4],
            // A013/S07_MyGame05.cpp:1588-1609) and reports whichever tribe dealt the most; Fenrir has no
            // per-tribe damage accumulator on MonsterEntity (that would mean plumbing tribe attribution through
            // every hit in CombatResolver, not just the killing blow), so this uses the killing blow's own
            // tribe instead -- a documented simplification, not a bug.
            zoneEventBroadcaster?.Value.AnnounceSymbolResolved(symbolIndex, killer.Tribe);

        if (killer is null)
            return; // no resolvable killer -- nothing to roll

        // C16: popup-event kill-streak counter, using the SAME drop-eligibility flag the generic loot pipeline
        // below already computes (tCheckPossibleDrop's normal branch -- killer not more than 9 levels above the
        // monster, with the martial-item/boss exemption) rather than a second, independently-derived gate --
        // see MonsterDropRoller.IsEligible and Zone.NotifyPopupEventMonsterKill's own remarks. Hoisted here,
        // ahead of that pipeline's own SkipGenericTiers branch below, so the popup counter still advances even
        // on a kill (identifiers 287/564-568/1407/etc.) that skips the generic tiers entirely.
        var dropEligible = MonsterDropRoller.IsEligible(monsterDefinition.Monster, killer.Level);
        zone.NotifyPopupEventMonsterKill(killer, dropEligible);

        // Valley of the Deceased (Zone 200/297/298/299) kill-race quota decrement -- no-op on every other map
        // (ValleyWarKillRegistry.RegisterMonsterKill gates on ValleyWarMapCatalog itself) and no-op outside that
        // schedule's own KillRace phase. Réf. C++ : Server/ts25zone/S07_MyGame02.cpp:3162-3170.
        valleyWarKillRegistry?.RegisterMonsterKill(zone.MapId, killer.Tribe);

        var partyMemberIds = partyRegistry?.GetMembers(killer.CharacterId);
        zone.GrantMonsterKillExperience(killer.CharacterId, monster.Template.RealLevel,
            monster.Template.GeneralExperience, partyMemberIds,
            monster.Template.PatExperience, monster.Template.Life);

        zone.ApplyQuestKillProgress(killer.CharacterId, monster.Template.MonsterId, partyMemberIds);

        ApplyTowerCpForPvmMilestone(zone, killer, monster.Template.RealLevel);

        bool KillerHasItem(int itemId)
        {
            return killer.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values.Any(s => s.ItemId == itemId) ||
                   killer.Inventory.GetContainer(ContainerMatrix.InventoryPage1).Values.Any(s => s.ItemId == itemId);
        }

        var killerQuest = new QuestProgress(killer.QuestStepPermanent, killer.QuestActiveFlag, killer.QuestSort,
            killer.QuestTargetPhase, killer.QuestKillCounter);

        var luck = (killer.Stats?.Luck ?? 0) * 10;

        // Boss/event drop tier (BossEventDropResolver, Server/ts25zone/S07_MyGame05.cpp:2333-2662) resolves
        // first, immediately before the generic pipeline -- see its own remarks and MonsterDropRoller's class
        // remarks for the ordering this ports. BossDropOutcome.None (a no-op) for every monster id outside its
        // fixed set, so this is dead weight for the overwhelming majority of kills.
        var demonLordKillTally = monster.Template.MonsterId == BossEventDropResolver.DemonLordMonsterId
            ? Interlocked.Increment(ref _demonLordKillTally)
            : 0;
        var bossOutcome = BossEventDropResolver.Resolve(monster.Template.MonsterId, demonLordKillTally, state.Random,
            worldData, _bossDropCatalog);

        ApplyBossDropSideEffects(zone, killer, bossOutcome);

        long? money;
        IReadOnlyList<DroppedItem> genericItems;
        if (bossOutcome.SkipGenericTiers)
        {
            // Several identifiers (287, 564-568, 1407) `return` before ever reaching DROP_MONEY in the legacy
            // source -- the whole generic pipeline, including money, must not run for this kill.
            money = null;
            genericItems = [];
        }
        else
        {
            // Premium-account drop-rate bonus (Server/ts25zone/S07_MyGame05.cpp:2171-2176, MonsterDropRoller's
            // own class remarks for the full citation chain): a plain greater-than-zero check on the killer's
            // stored premium-expiry timestamp, never re-compared against the current time here -- expiry itself
            // is caught elsewhere by SupportSkillTimeUpRatioMaintenanceSystem's own per-minute pass, which zeroes
            // PremiumExpireUtc once it lapses.
            var result = state.DropRoller.Roll(monsterDefinition, killer.Level, killer.Tribe, luck, killerQuest,
                KillerHasItem, killer.PremiumExpireUtc > 0);
            money = result.Money;

            // Tail-span tiers (MonsterDropTailResolver, Server/ts25zone/S07_MyGame05.cpp:3391-3427 "CP Gift
            // Card", :3515-3582 "LOD Rebirth Item"): sit after MonsterDropRoller's own documented :2999
            // boundary, still gated by the same early-return (SkipGenericTiers, this branch) that guards
            // every other generic tier above. Reuses the SAME dropEligible flag computed above (for the C16
            // popup-event trigger) rather than a second call to MonsterDropRoller.IsEligible.
            var cpGiftItems = MonsterDropTailResolver.ResolveCpGiftCard(dropEligible,
                monster.Template.MonsterId, zone.IsZone241TypeZone, killer.Level2,
                // mCheckZone126TypeServer's CP-gift 50-vs-25 base-rate knob, now resolved from this shard's own
                // config-driven Zone126-type classification (Zone.ZoneTypeClassification.cs -- the Fenrir
                // translation of the legacy boot-time server-number gate, Server/ts25zone/S07_MyGame05.cpp:3402).
                // NOTE: the sibling zone.IsZone039TypeZone flag is deliberately NOT passed here -- Zone039-type
                // gates a separate, unported mount/pet event tier (:3197-3204), not this rate.
                zone.IsZone126TypeZone, state.Random);
            var rebirthItems = MonsterDropTailResolver.ResolveRebirthItem(monster.Template.MonsterId, state.Random);

            genericItems = cpGiftItems.Count == 0 && rebirthItems.Count == 0
                ? result.Items
                : [.. result.Items, .. cpGiftItems, .. rebirthItems];
        }

        if (money is { } amount)
            // Deliberately NOT routed through Zone.CreditMonsterKillTribeTax: in production ts25zone, a
            // monster-kill money grant never reaches AddTribeBankInfo3 (the 9% tribe-bank tax). The only
            // call site of that function is ProcessForDropItem's DP_MN_TO_WD/currency-item branch
            // (S07_MyGame03.cpp:486-494), and the live DROP_MONEY block never calls ProcessForDropItem for
            // its own currency grant -- the one line that would have (S07_MyGame05.cpp:2689) is commented
            // out. See Zone.TribeBankTax.cs's own remarks for the full citation set and the still-live 1%
            // NPC-service tax this does not affect.
            //
            // C14 reconciliation: the C14 contract's money->world reshape (the 15% ground reduction + the 9%
            // tribe-bank deposit + the tower-silver add-back) IS now ported -- as the pure, reusable
            // InventoryToWorldDropPolicy.ReshapeGroundDrop money branch (which returns TribeBankDepositAmount
            // for a Zone.CreditMonsterKillTribeTax caller). It is NOT fired here because the monster-kill money
            // GRANT (a direct award to the killer, not a money->world ground drop) never enters ProcessForDropItem
            // in the live LNW33 build per :2689 above -- so crediting from here would manufacture tribe-bank
            // income production never generates AND break MonsterSpawnSchedulerTribeBankTaxTests. If
            // cpp-zone-gameplay-analyst confirms :2689 is in fact live for monster kills, the wiring is a one-line
            // Zone.CreditMonsterKillTribeTax(killer.Tribe, reshape.TribeBankDepositAmount) plus deleting that test.
            zone.QueueMoneyGrant(killer.CharacterId, amount);

        foreach (var publicItem in bossOutcome.PublicItems)
            // Ownerless/public loot (identifier 576's Labyrinth Key): empty master/party name makes it
            // immediately claimable by anyone (GroundItemEntity.IsClaimableBy rule 3), unlike every other drop
            // in this method which is attributed to the killer below.
            zone.SpawnGroundItem(publicItem.ItemId, publicItem.Quantity, monster.PosX, monster.PosY, monster.PosZ,
                "", "", 0, monster.InstanceId);

        if (bossOutcome.Items.Count == 0 && genericItems.Count == 0)
            return;

        var (partyName, dropSort) = ResolvePartyDrop(zone, killer, partyMemberIds);

        // C14 reconciliation: the ELITE_NOTICE code-2000 "notable drop" relay IS now ported -- as the pure
        // Inventory.EliteDropNoticeResolver (type codes 55/56/0/1/2, STRUCT.h:1306-1313). It is NOT fired for
        // these monster->world drops because a monster->world drop never passes the notice's "show name" test in
        // the live build: the elite-tier auto-announce block is commented out in the source
        // (S07_MyGame03.cpp:650-717), and neither the pets-1002..1005 (treasure-chest-only) nor the pvp-forced
        // branch applies to ordinary monster loot -- so EliteDropNoticeResolver.Resolve(MonsterToWorld, ...)
        // returns null for every item here. The resolver is wired by the origins that DO announce (the
        // treasure-chest open path and the pvp-death path), each owned by its own future handler.
        foreach (var item in bossOutcome.Items.Concat(genericItems))
            // Zone-241 "LOD" personal-dungeon loot tag (Server/ts25zone/S07_MyGame03.cpp:599): a dying
            // personal boss carries its own instance id (Zone.SummonPersonalBoss); every ordinary monster's
            // InstanceId is null, so this is a no-op tag for every non-personal-instance drop.
            zone.SpawnGroundItem(item.ItemId, item.Quantity, monster.PosX, monster.PosY, monster.PosZ,
                killer.Name, partyName, dropSort, monster.InstanceId);
    }

    /// <summary>
    ///     Applies <see cref="BossEventDropResolver" />'s CP/War Point/Blood Point grants and the identifier-1407
    ///     kill announcement -- the parts of its outcome that need <see cref="Zone" /> I/O rather than plain data,
    ///     which is why they aren't folded into the (pure) resolver itself.
    /// </summary>
    private static void ApplyBossDropSideEffects(Zone zone, PlayerRuntimeState killer, BossDropOutcome outcome)
    {
        if (outcome.ContributionPointsGranted != 0)
            zone.GrantContributionPoints(killer.CharacterId, outcome.ContributionPointsGranted);

        if (outcome.WarPointsGranted != 0)
            zone.GrantWarPoints(killer.CharacterId, outcome.WarPointsGranted);

        if (outcome.BloodPointsGranted != 0)
            zone.GrantBloodPoints(killer.CharacterId, outcome.BloodPointsGranted);

        if (outcome.AnnounceEliteBossDefeat)
            zone.AnnounceEliteBossDefeated(killer.Tribe, killer.Name);
    }

    /// <summary>
    ///     <c>ProcessForDropItem</c>'s <c>DP_MN_TO_WD</c> branch (<c>S07_MyGame03.cpp:622-625</c>): a partied
    ///     killer's drop gets DropSort=1 (see <see cref="Loot.GroundItemEntity.IsClaimableBy" />'s party-share
    ///     window) and PartyName set to the party's own name -- legacy's <c>aPartyName</c> is always the
    ///     leader's own character name (<c>S04_MyWork02.cpp:9720-9721</c>: copied onto every member at
    ///     join/create time), never a player-chosen string, so <see cref="PartyRegistry" />'s leader-first
    ///     member list (<see cref="Social.Party.Party.LeaderId" />) is exactly the same identity.
    /// </summary>
    private static (string PartyName, int DropSort) ResolvePartyDrop(Zone zone, PlayerRuntimeState killer,
        IReadOnlyList<int>? partyMemberIds)
    {
        if (partyMemberIds is not { Count: > 0 } members)
            return ("", 0);

        var leaderId = members[0];
        if (leaderId == killer.CharacterId)
            return (killer.Name, 1);

        // Leader is almost always in the same zone (parties hunt together); if not resolvable here (e.g.
        // mid zone-transfer), fall back to the killer's own name -- still non-empty, still marks the drop
        // party-shareable, just not byte-identical to legacy's exact string in this rare edge case.
        return zone.TryGetPlayer(leaderId, out var leader) && leader is not null
            ? (leader.Name, 1)
            : (killer.Name, 1);
    }

    /// <summary>
    ///     Tower CP-for-PvM consumption hook (<c>ProcessAttack03</c>'s post-kill CP milestone section,
    ///     <see cref="TowerCpForPvmMilestone" />): advances the killer's personal 1000-kill counter regardless of
    ///     whether <paramref name="killer" />'s tribe currently owns a CP tower, and on the 1000th kill grants
    ///     <see cref="TowerCpForPvmMilestone.BaseKillCp" /> plus that tribe's flat CP-for-PvM tower bonus (0 if
    ///     <see cref="towerWar" /> is unavailable, e.g. some test call sites).
    /// </summary>
    private void ApplyTowerCpForPvmMilestone(Zone zone, PlayerRuntimeState killer, int monsterRealLevel)
    {
        var registration = TowerCpForPvmMilestone.RegisterKill(killer.TowerCpMilestoneCounter, killer.Level,
            killer.Level2, monsterRealLevel);
        killer.TowerCpMilestoneCounter = registration.UpdatedCounter;

        if (!registration.MilestoneReached)
            return;

        var towerBonus = towerWar?.GetTribeBonus(killer.Tribe).CpForPvmBonus ?? 0;
        zone.GrantContributionPoints(killer.CharacterId, TowerCpForPvmMilestone.ComputeReward(towerBonus));
    }

    /// <summary>
    ///     Maps a "Holy Stone" guardian's <c>SpecialType</c> to
    ///     <see cref="World.ZoneWar.ZoneEventBroadcaster.AnnounceSymbolResolved" />'s symbolIndex (0-3 = one
    ///     tribe's own slot, 4 = the neutral monster-guarded slot) -- null for every other monster. These are
    ///     structural legacy constants (<c>A013</c>'s own <c>SpecialType</c> switch, <c>S07_MyGame05.cpp:1568-1587</c>),
    ///     not DB-catalogued data: monsters 601-605 ("Holy Stone", seeded in <c>world.Monsters</c>) are the only
    ///     ones that ever carry these values.
    /// </summary>
    private static byte? TribeSymbolIndexOf(byte specialType)
    {
        return specialType switch
        {
            11 => 0,
            12 => 1,
            13 => 2,
            28 => 3,
            14 => 4, // neutral, monster-guarded slot -- WorldStateService.ResolveMonsterSymbol
            _ => null
        };
    }
}

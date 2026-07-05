using System.Collections.Concurrent;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Inventory;
using Fenrir.Application.Game.Quests;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Social.Party;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Application.Game.World.ZoneWar;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.World.Monsters;

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
///         state machine) and <c>SummonGuard</c>/<c>SummonTribeSymbol</c> (hardcoded per-tribe-territory
///         coordinates that depend on an unmodeled territory-ownership system) -- both summon their monsters
///         outside <c>world.MonsterSpawnRegions</c> entirely, so neither is reachable from this scheduler's own
///         per-region pool. The dungeon <c>mNumber</c>-forced-to-20 instance-population override is also not
///         ported: it depends on a dungeon/instance flag this schema does not catalog on <c>world.Zones</c> yet.
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
/// </remarks>
public sealed class MonsterSpawnScheduler(
    WorldDataCache worldData,
    Func<Random>? randomFactory = null,
    PartyRegistry? partyRegistry = null,
    Lazy<ZoneEventBroadcaster>? zoneEventBroadcaster = null,
    MonsterBossRespawnTracker? bossRespawnTracker = null)
    : ISimulationSystem
{
    /// <summary>
    ///     A fresh <see cref="System.Random" /> per zone -- never shared across zones, since different zones
    ///     tick concurrently and <see cref="System.Random" /> is not thread-safe. Tests may inject a factory
    ///     returning a seeded <see cref="Random" /> for deterministic rolls.
    /// </summary>
    private readonly Func<Random> _randomFactory = randomFactory ?? (static () => new Random());

    private readonly ConcurrentDictionary<short, MonsterZoneSpawnState> _stateByZone = new();

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var state = _stateByZone.GetOrAdd(zone.MapId, _ => BuildState(zone.MapId));

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

    private MonsterZoneSpawnState BuildState(short mapId)
    {
        var regions = worldData.ZonesByNumber.TryGetValue(mapId, out var zoneDef)
            ? zoneDef.MonsterSpawnRegions
            : [];

        var slots = new List<MonsterSpawnSlot>();
        var nextServerIndex = 1;
        var now = DateTime.UtcNow;
        foreach (var region in regions)
        {
            if (region.MonsterId is not { } monsterId ||
                !worldData.MonstersById.TryGetValue(monsterId, out var monsterDefinition))
                continue; // the cache is already filtered of these (WorldDataFilterStats) -- defensive only

            var slotCount = Math.Max(0, region.Number);
            for (var i = 0; i < slotCount; i++)
            {
                var slot = new MonsterSpawnSlot
                {
                    Region = region,
                    Monster = monsterDefinition.Monster,
                    ServerIndex = nextServerIndex++
                };

                // A restart must resume a persisted boss's cooldown instead of popping it back in at tick 1 --
                // see MonsterBossRespawnTracker's own remarks.
                if (IsPersistedBossMonster(monsterId) && bossRespawnTracker is { } tracker &&
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
    ///     Random point inside the region's disk, Y resolved via <see cref="Zone.Geometry" /> when available, else the
    ///     region's own recorded Y.
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

        if (zone.Geometry is { } geometry && geometry.TryGetGroundHeight(x, z, out var groundY))
            y = groundY;

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

        var partyMemberIds = partyRegistry?.GetMembers(killer.CharacterId);
        zone.GrantMonsterKillExperience(killer.CharacterId, monster.Template.RealLevel,
            monster.Template.GeneralExperience, partyMemberIds);

        zone.ApplyQuestKillProgress(killer.CharacterId, monster.Template.MonsterId);

        bool KillerHasItem(int itemId)
        {
            return killer.Inventory.GetContainer(ContainerMatrix.InventoryPage0).Values.Any(s => s.ItemId == itemId) ||
                   killer.Inventory.GetContainer(ContainerMatrix.InventoryPage1).Values.Any(s => s.ItemId == itemId);
        }

        var killerQuest = new QuestProgress(killer.QuestStepPermanent, killer.QuestActiveFlag, killer.QuestSort,
            killer.QuestTargetPhase, killer.QuestKillCounter);

        var luck = (killer.Stats?.Luck ?? 0) * 10;
        var result = state.DropRoller.Roll(monsterDefinition, killer.Level, killer.Tribe, luck, killerQuest,
            KillerHasItem);

        if (result.Money is { } amount)
            zone.QueueMoneyGrant(killer.CharacterId, amount);

        if (result.Items.Count == 0)
            return;

        var (partyName, dropSort) = ResolvePartyDrop(zone, killer, partyMemberIds);
        foreach (var item in result.Items)
            zone.SpawnGroundItem(item.ItemId, item.Quantity, monster.PosX, monster.PosY, monster.PosZ,
                killer.Name, partyName, dropSort);
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

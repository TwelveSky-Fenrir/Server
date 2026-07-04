using System.Collections.Concurrent;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Social.Party;
using Fenrir.Application.Game.World.Loot;
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
///         Not ported: <c>SummonBossMonster</c> (boss-table state machine), <c>SummonGuard</c>/
///         <c>SummonTribeSymbol</c> (hardcoded per-server coordinates), the dungeon <c>mNumber</c>-forced-to-20
///         override, the monster-746 fixed cooldown, and the disk-persisted Yanggok boss timers. This system
///         covers only the generic per-region "normal monster" population.
///     </para>
/// </remarks>
public sealed class MonsterSpawnScheduler(
    WorldDataCache worldData,
    Func<Random>? randomFactory = null,
    PartyRegistry? partyRegistry = null)
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
            // Every configured slot pops on the very first tick, unconditionally, before any respawn timer logic applies.
            state.InitialPopDone = true;
            foreach (var slot in state.Slots)
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
            if (!slot.Alive && slot.RespawnTicksRemaining <= 0)
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
        foreach (var region in regions)
        {
            if (region.MonsterId is not { } monsterId ||
                !worldData.MonstersById.TryGetValue(monsterId, out var monsterDefinition))
                continue; // the cache is already filtered of these (WorldDataFilterStats) -- defensive only

            var slotCount = Math.Max(0, region.Number);
            for (var i = 0; i < slotCount; i++)
                slots.Add(new MonsterSpawnSlot
                {
                    Region = region,
                    Monster = monsterDefinition.Monster,
                    ServerIndex = nextServerIndex++
                });
        }

        var random = _randomFactory();
        return new MonsterZoneSpawnState
        {
            Slots = slots,
            DropRoller = new MonsterDropRoller(worldData, random),
            Random = random
        };
    }

    /// <summary>Random point inside the region's disk, Y resolved via <see cref="Zone.Geometry" /> when available, else the region's own recorded Y.</summary>
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
                slot.RespawnTicksRemaining = RollRespawnTicks(slot.Monster, state.Random);
            }

            ProcessDeath(zone, state, death!);
        }
    }

    /// <summary><c>mSummonTime[0..1]</c> is in seconds (<c>S10_MySummon.cpp:1845</c>) -- converted to legacy ticks here.</summary>
    private static int RollRespawnTicks(MonsterRowDto monster, Random random)
    {
        var minSeconds = monster.SummonTime1;
        var maxSeconds = monster.SummonTime2;
        var seconds = maxSeconds > minSeconds ? minSeconds + random.Next(maxSeconds - minSeconds + 1) : minSeconds;
        return SimulationClock.ToWholeLegacyTicks(TimeSpan.FromSeconds(Math.Max(0, seconds)));
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

        if (killer is null)
            return; // no resolvable killer -- nothing to roll

        var partyMemberIds = partyRegistry?.GetMembers(killer.CharacterId);
        zone.GrantMonsterKillExperience(killer.CharacterId, monster.Template.RealLevel,
            monster.Template.GeneralExperience, partyMemberIds);

        zone.ApplyQuestKillProgress(killer.CharacterId, monster.Template.MonsterId);

        var luck = (killer.Stats?.Luck ?? 0) * 10;
        var result = state.DropRoller.Roll(monsterDefinition, killer.Level, killer.Tribe, luck);

        if (result.Money is { } amount)
            zone.QueueMoneyGrant(killer.CharacterId, amount);

        if (result.Items.Count == 0)
            return;

        // DropSort always 0 (exclusive to the killer until the free-for-all window) -- see
        // GroundItemEntity.IsClaimableBy's remarks on the not-yet-triggerable party-share branch.
        foreach (var item in result.Items)
            zone.SpawnGroundItem(item.ItemId, item.Quantity, monster.PosX, monster.PosY, monster.PosZ,
                killer.Name, "", 0);
    }
}

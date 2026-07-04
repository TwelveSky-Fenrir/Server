using System.Collections.Concurrent;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.World.Loot;
using Fenrir.Data.World;

namespace Fenrir.Application.Game.World.Monsters;

/// <summary>
///     One pool slot for the Nth copy of one <c>world.MonsterSpawnRegions</c> row (report
///     ServerDocs/30_Fenrir_ServerLogic/05_game_mechanics.md §1: "parcourt à plat région × mNumber"). The
///     <see cref="ServerIndex" /> is assigned ONCE and stays stable for the zone's whole lifetime (Fenrir has
///     no fixed-size shared-memory pool to recycle slots out of, unlike the legacy's
///     <c>shmMONSTER_OBJECT[MAX_MONSTER_OBJECT_NUM]</c> -- a documented simplification, not client-observable
///     since the wire's <c>ServerIndex</c> field carries no range constraint of its own).
/// </summary>
internal sealed class MonsterSpawnSlot
{
    public required MonsterSpawnRegionRowDto Region { get; init; }
    public required MonsterRowDto Monster { get; init; }
    public required int ServerIndex { get; init; }
    public bool Alive { get; set; }
    public int RespawnTicksRemaining { get; set; }
}

/// <summary>Everything this scheduler needs to remember for ONE zone -- see <see cref="MonsterSpawnScheduler" />'s remarks on why this lives keyed by zone rather than as the system's own instance fields.</summary>
internal sealed class MonsterZoneSpawnState
{
    public required List<MonsterSpawnSlot> Slots { get; init; }
    public required MonsterDropRoller DropRoller { get; init; }
    public required Random Random { get; init; }
    public bool InitialPopDone { get; set; }
    public int TicksSinceLastScan { get; set; }
}

/// <summary>
///     Owns every zone's monster spawn-region pool: initial pop, the ~10 s respawn scan (report 05 §1/§11:
///     "SummonMonster() toutes les ~10 s, tick %20"), and the fallout of a kill (loot roll + ground-item
///     spawn + <see cref="Zone.GrantMonsterKillExperience" /> + arming that slot's own respawn timer).
/// </summary>
/// <remarks>
///     A single DI SINGLETON shared across every <see cref="Zone" /> (<see cref="ZoneRegistry" />'s own
///     documented convention: "<c>ISimulationSystem</c> instances are stateless singletons that operate on
///     whichever Zone they're handed") -- so the actual per-zone mutable state (spawn slots, timers, that
///     zone's own drop-roll RNG) lives in a <see cref="MonsterZoneSpawnState" /> keyed by
///     <see cref="Zone.MapId" /> in <see cref="_stateByZone" />, built lazily on that zone's own FIRST
///     <see cref="Simulate" /> call (never racy across zones: each zone's tick is its own single thread, and a
///     <see cref="ConcurrentDictionary{TKey,TValue}" /> key is only ever first-built by that SAME zone's own
///     thread).
///     <para>
///     NOT ported from report 05 §1 (explicit open issues, not silently dropped): <c>SummonBossMonster</c>
///     (boss-table state machine, ~3 h cooldown), <c>SummonGuard</c>/<c>SummonTribeSymbol</c> (200+ lines of
///     hardcoded per-server coordinates -- report 05 itself flags these as needing a NEW table that "n'existe
///     pas encore"), the dungeon <c>mNumber</c>-forced-to-20 override, the monster-746 fixed 240 s cooldown,
///     and the disk-persisted Yanggok boss timers (564-568). This system covers ONLY the generic per-region
///     "normal monster" population report 05 calls the cruising regime.
///     </para>
/// </remarks>
public sealed class MonsterSpawnScheduler(
    WorldDataCache worldData,
    Func<Random>? randomFactory = null,
    Social.Party.PartyRegistry? partyRegistry = null)
    : ISimulationSystem
{
    private readonly ConcurrentDictionary<short, MonsterZoneSpawnState> _stateByZone = new();

    /// <summary>
    ///     Production default: a FRESH <see cref="System.Random" /> per zone (unseeded) -- never one instance
    ///     shared across zones, since different zones tick concurrently on their own threads and
    ///     <see cref="System.Random" /> is not safe for concurrent use from multiple threads. Tests may inject
    ///     a factory that returns a seeded <see cref="Random" /> for deterministic spawn scatter/respawn-timer
    ///     rolls (same rationale as <see cref="Combat.IRandomSource" />'s own injectability on <see cref="Zone" />).
    /// </summary>
    private readonly Func<Random> _randomFactory = randomFactory ?? (static () => new Random());

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var state = _stateByZone.GetOrAdd(zone.MapId, _ => BuildState(zone.MapId));

        if (!state.InitialPopDone)
        {
            // Report 05 §1: "Pop initial au boot ... premier passage de SummonMonster() = pop immédiat" --
            // every configured slot pops on the very first tick, unconditionally, before any respawn timer
            // logic applies at all.
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

    /// <summary>Slot count for a zone that has already ticked at least once (0 if it hasn't, or hosts no monster spawn regions) -- test/inspection surface.</summary>
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

    /// <summary>
    ///     Random point inside the region's disk (report 05 §1: "position aléatoire dans le disque mRADIUS
    ///     validée contre le navmesh"), Y resolved via <see cref="Zone.Geometry" /> when available -- falls
    ///     back to the region's own recorded Y (documented M1-consistent placeholder, matching
    ///     <see cref="Movement.MovementRules" />'s own posture when no <c>.WM</c> is loaded).
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
                slot.RespawnTicksRemaining = RollRespawnTicks(slot.Monster, state.Random);
            }

            ProcessDeath(zone, state, death!);
        }
    }

    /// <summary>
    ///     <c>mSummonTime[0..1]</c> is in SECONDS (verified: <c>S10_MySummon.cpp:1845</c> divides a
    ///     millisecond tick delta by 1000.0f before comparing against it directly) -- converted to legacy
    ///     ticks (÷0.5 s) for this scheduler's own tick-counted countdown.
    /// </summary>
    private static int RollRespawnTicks(MonsterRowDto monster, Random random)
    {
        var minSeconds = monster.SummonTime1;
        var maxSeconds = monster.SummonTime2;
        var seconds = maxSeconds > minSeconds ? minSeconds + random.Next(maxSeconds - minSeconds + 1) : minSeconds;
        return SimulationClock.ToWholeLegacyTicks(TimeSpan.FromSeconds(Math.Max(0, seconds)));
    }

    /// <summary>
    ///     Loot pipeline + XP-grant seam for one kill (report 05 §5). Runs entirely on the zone's own tick
    ///     thread (single-writer invariant) -- money grants are handed to <see cref="Zone.QueueMoneyGrant" />
    ///     (a fire-and-forget-safe queue a dedicated background flusher drains, see
    ///     <see cref="MonsterLootFlushHost" />) rather than awaited here, since <see cref="Zone.Tick" />
    ///     is fully synchronous and must never block on SQL I/O.
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
            return; // no resolvable killer -- report 04/05's own drop/XP pipeline both key off the killer, nothing to roll

        // Already-built V3 seam (Zone.GrantMonsterKillExperience, report 05 §5 ProcessForExp/§6 ProcessForExperience) --
        // this pass just supplies the two plain template values it needs, plus (Phase C/V6 Social) the
        // killer's full party roster so Zone can pay the flat present-member bonus -- see that method's
        // own remarks for why party membership never changes the killer's OWN base gain above.
        var partyMemberIds = partyRegistry?.GetMembers(killer.CharacterId);
        zone.GrantMonsterKillExperience(killer.CharacterId, monster.Template.RealLevel,
            monster.Template.GeneralExperience, partyMemberIds);

        // Server Logic V9 Progression: the SAME kill-death seam as the XP grant above (report 04 §5's own
        // hook, verified S07_MyGame02.cpp:2493-2564) -- a no-op unless the killer's active quest is a
        // kill-type (qSort 1/5) targeting THIS monster id.
        zone.ApplyQuestKillProgress(killer.CharacterId, monster.Template.MonsterId);

        var luck = (killer.Stats?.Luck ?? 0) * 10;
        var result = state.DropRoller.Roll(monsterDefinition, killer.Level, killer.Tribe, luck);

        if (result.Money is { } amount)
            zone.QueueMoneyGrant(killer.CharacterId, amount);

        if (result.Items.Count == 0)
            return;

        // DropSort always 0 (exclusive to the killer's own name until the universal 30 s free-for-all window)
        // -- PlayerRuntimeState has no party/group membership field yet (a different, not-yet-built domain),
        // so the legacy's DropSort==1 "killer was in a party" branch can never trigger here. See
        // GroundItemEntity.IsClaimableBy's own remarks: the 10 s party-share rule is still implemented
        // correctly and activates automatically the day party membership is threaded through.
        foreach (var item in result.Items)
            zone.SpawnGroundItem(item.ItemId, item.Quantity, monster.PosX, monster.PosY, monster.PosZ,
                killer.Name, partyName: "", dropSort: 0);
    }
}

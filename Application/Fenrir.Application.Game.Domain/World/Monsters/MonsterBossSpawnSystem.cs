using System.Collections.Concurrent;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.Monsters;

/// <summary>
///     Drives the legacy <c>SummonBossMonster</c> 3-state boss-respawn machine (Reload -&gt; Check -&gt; Wait-3h
///     -&gt; Reload) per hosted zone: every 20 legacy ticks it advances exactly one state, and on Reload it fills
///     up to 5 of a reserved 20-slot boss window from one randomly-picked candidate, re-cycling every ~3 hours
///     (<c>Server/ts25zone/S10_MySummon.cpp:1066-1216</c>). Registered as an <see cref="ISimulationSystem" /> and
///     shared across every <see cref="Zone" /> this process ticks, so per-zone mutable state lives in
///     <see cref="_stateByZone" /> keyed by <see cref="Zone.MapId" /> -- built lazily on that zone's first
///     <see cref="Simulate" /> call, never racy since each zone's tick is its own single thread (same shape as
///     <see cref="MonsterSpawnScheduler" />).
/// </summary>
/// <remarks>
///     <para>
///         Covers exactly the boss-table state machine <see cref="MonsterSpawnScheduler" /> explicitly does NOT
///         (see its own class remarks): a separate 3-hour boss-respawn cycle operating on its own reserved
///         boss-object slot window (<see cref="MonsterBossSpawnMachine.DefaultBossSlotBase" />..+20), disjoint
///         from the generic per-region population pool. Deaths of these bosses are still drained (and their loot
///         rolled) by <see cref="MonsterSpawnScheduler.DrainDeaths" /> like any other monster -- its slot lookup
///         simply finds no matching region slot (this window is outside its <c>ServerIndex</c> range) and arms no
///         region respawn timer, which is correct: this machine, not the scheduler, owns re-filling a freed boss
///         slot on its next 3-hour Reload. Slot occupancy is therefore a pull check against
///         <see cref="Zone.TryGetMonster" /> (is my boss still in the live pool?), needing no death-queue
///         subscription of its own.
///     </para>
///     <para>
///         Inert on every map until a verified <see cref="MonsterBossSummonCatalog" /> replaces the default
///         <see cref="MonsterBossSummonCatalog.Empty" />: the concrete per-zone boss ids are an uncertified
///         data-pipeline open question (see that catalog's remarks). The catalog is therefore the eligibility
///         gate too -- a zone with no catalog entry (the default for every map) never builds per-zone state and
///         never advances the machine. This subsumes the legacy call-site gating (the routine runs only on the
///         ZONE039-typed branch and the generic fall-through branch, never on a specialized RvR zone-type branch,
///         <c>S07_MyGame01.cpp:2895-2905,3033-3041</c>): a specialized-branch zone that must not run the machine
///         is simply given no catalog entry.
///     </para>
/// </remarks>
public sealed class MonsterBossSpawnSystem(
    WorldDataCache worldData,
    MonsterBossSummonCatalog? catalog = null,
    Func<Random>? randomFactory = null) : ISimulationSystem
{
    private readonly MonsterBossSummonCatalog _catalog = catalog ?? MonsterBossSummonCatalog.Empty;
    private readonly Func<Random> _randomFactory = randomFactory ?? (static () => new Random());
    private readonly ConcurrentDictionary<short, MonsterBossSpawnZoneState> _stateByZone = new();

    public void Simulate(Zone zone, int legacyTicksElapsed)
    {
        var candidates = _catalog.CandidatesFor(zone.MapId);
        if (candidates.Length < 1)
            return; // inert: this map ships no boss candidates (the default for every map)

        var state = _stateByZone.GetOrAdd(zone.MapId, _ => BuildState(zone, candidates));
        MonsterBossSpawnMachine.Advance(state, legacyTicksElapsed);
    }

    /// <summary>
    ///     Number of reserved boss slots currently occupied by a live boss on <paramref name="mapId" /> (test/diagnostics
    ///     helper).
    /// </summary>
    public int LiveBossCountFor(Zone zone)
    {
        if (_catalog.CandidatesFor(zone.MapId).Length < 1)
            return 0;

        var live = 0;
        for (var i = 0; i < MonsterBossSpawnMachine.BossSlotWindowSize; i++)
            if (zone.TryGetMonster(MonsterBossSpawnMachine.DefaultBossSlotBase + i, out var monster) &&
                monster is not null)
                live++;
        return live;
    }

    private MonsterBossSpawnZoneState BuildState(Zone zone, ImmutableArray<MonsterBossSummonCandidate> candidates)
    {
        var random = _randomFactory();
        return new MonsterBossSpawnZoneState
        {
            Candidates = candidates,
            SlotBase = MonsterBossSpawnMachine.DefaultBossSlotBase,
            Random = random,
            Sink = new ZoneBossSpawnSink(zone, worldData, random)
        };
    }

    /// <summary>
    ///     Production <see cref="IMonsterBossSpawnSink" /> for one zone: resolves boss ids against
    ///     <see cref="WorldDataCache" />, checks live boss-slot occupancy against the zone's monster pool, and
    ///     spawns a boss at a candidate's stored coordinate verbatim. Tick-thread-only (one per zone-state, never
    ///     shared across zones), so its <see cref="_resolvedTemplate" /> cache carries no cross-thread risk.
    /// </summary>
    private sealed class ZoneBossSpawnSink(Zone zone, WorldDataCache worldData, Random random) : IMonsterBossSpawnSink
    {
        private MonsterRowDto? _resolvedTemplate;

        public bool TryResolveCandidate(int monsterId)
        {
            if (worldData.MonstersById.TryGetValue(monsterId, out var definition))
            {
                _resolvedTemplate = definition.Monster;
                return true;
            }

            _resolvedTemplate = null;
            return false;
        }

        public bool IsSlotFree(int serverIndex)
        {
            // A boss occupies its slot only while it is still in the live monster pool. On death,
            // Zone.TryDamageMonster removes it from _monsters (any thread) and MonsterSpawnScheduler.DrainDeaths
            // finishes the tick-side cleanup + loot -- either way, a dead boss is gone from here, freeing the slot.
            return !zone.TryGetMonster(serverIndex, out var monster) || monster is null;
        }

        public void SpawnBoss(int serverIndex, in MonsterBossSummonCandidate candidate)
        {
            var template = _resolvedTemplate;
            if (template is null)
                return; // defensive: SpawnBoss is only ever reached after a successful TryResolveCandidate

            // Stored-location boss path (S10_MySummon.cpp:753-762): coordinate copied VERBATIM -- no radius
            // dispersion, no ground-altitude re-clamp. This is the load-bearing divergence from
            // MonsterSpawnScheduler.Spawn's calculated-location path, which scatters within the radius and
            // ground-snaps. The record's Radius is used only as the (unused-by-leash-checks) leash bound every
            // spawn call site already populates, matching MonsterSpawnScheduler.Spawn.
            var leash = MathF.Max(candidate.Radius, 1f);
            var entity = MonsterEntity.Create(serverIndex, zone.NextMonsterUniqueNumber(), template, serverIndex,
                candidate.X, candidate.Y, candidate.Z, leash);

            // Random facing at spawn (S10_MySummon.cpp:772-817). Initial AI sort 0 (post-spawn wait),
            // follow/target seeding, and the special-sort classification are already applied by
            // MonsterEntity.Create.
            entity.Heading = (float)(random.NextDouble() * (Math.PI * 2));

            zone.SpawnMonster(entity);
        }
    }
}

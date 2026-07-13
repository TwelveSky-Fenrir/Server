using System.Collections.Concurrent;
using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.Monsters;

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
            return;

        var state = _stateByZone.GetOrAdd(zone.MapId, _ => BuildState(zone, candidates));
        MonsterBossSpawnMachine.Advance(state, legacyTicksElapsed);
    }

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
            return !zone.TryGetMonster(serverIndex, out var monster) || monster is null;
        }

        public void SpawnBoss(int serverIndex, in MonsterBossSummonCandidate candidate)
        {
            var template = _resolvedTemplate;
            if (template is null)
                return;

            var entity = MonsterEntity.Create(serverIndex, zone.NextMonsterUniqueNumber(), template, serverIndex,
                candidate.X, candidate.Y, candidate.Z);

            entity.Heading = (float)(random.NextDouble() * (Math.PI * 2));

            zone.SpawnMonster(entity);
        }
    }
}

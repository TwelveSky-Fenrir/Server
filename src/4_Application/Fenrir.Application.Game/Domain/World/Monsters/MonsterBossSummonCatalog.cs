using System.Collections.Frozen;
using System.Collections.Immutable;
using Fenrir.Application.Game.GameData;

namespace Fenrir.Application.Game.Domain.World.Monsters;

public sealed class MonsterBossSummonCatalog
{
    private readonly FrozenDictionary<short, ImmutableArray<MonsterBossSummonCandidate>> _byMapId;

    public MonsterBossSummonCatalog(IReadOnlyDictionary<short, ImmutableArray<MonsterBossSummonCandidate>> byMapId)
    {
        _byMapId = byMapId.ToFrozenDictionary();
    }

    public static MonsterBossSummonCatalog Empty { get; } =
        new(FrozenDictionary<short, ImmutableArray<MonsterBossSummonCandidate>>.Empty);

    public static MonsterBossSummonCatalog BuildFrom(WorldDataCache worldData)
    {
        var byMapId = new Dictionary<short, ImmutableArray<MonsterBossSummonCandidate>>();

        foreach (var (mapId, zoneDef) in worldData.ZonesByNumber)
        {
            if (zoneDef.BossMonsterSpawnRegions.Length < 1)
                continue;

            var candidates = new List<MonsterBossSummonCandidate>(zoneDef.BossMonsterSpawnRegions.Length);
            foreach (var region in zoneDef.BossMonsterSpawnRegions)
            {
                if (region.MonsterId is not { } monsterId)
                    continue;

                candidates.Add(new MonsterBossSummonCandidate(monsterId, region.LocationX, region.LocationY,
                    region.LocationZ, region.Radius, region.Number));
            }

            var normalized = NormalizeBossRows(candidates);
            if (normalized.Length > 0)
                byMapId[mapId] = normalized;
        }

        return new MonsterBossSummonCatalog(byMapId);
    }

    public ImmutableArray<MonsterBossSummonCandidate> CandidatesFor(short mapId)
    {
        return _byMapId.TryGetValue(mapId, out var pool) ? pool : [];
    }

    public static ImmutableArray<MonsterBossSummonCandidate> NormalizeBossRows(
        IReadOnlyList<MonsterBossSummonCandidate> rows)
    {
        var count = rows.Count;
        if ((count & 1) == 1)
            count--;

        if (count < 1)
            return [];

        var builder = ImmutableArray.CreateBuilder<MonsterBossSummonCandidate>(count);
        for (var i = 0; i < count; i++)
            builder.Add(rows[i]);
        return builder.MoveToImmutable();
    }
}

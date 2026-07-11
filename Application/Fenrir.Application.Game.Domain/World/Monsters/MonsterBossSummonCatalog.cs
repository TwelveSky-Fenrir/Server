using System.Collections.Frozen;
using System.Collections.Immutable;

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

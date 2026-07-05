using System.Collections.ObjectModel;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.World.Monsters;

/// <summary>In-memory stand-in for <see cref="IMonsterBossRespawnTimerRepository" />.</summary>
internal sealed class FakeMonsterBossRespawnTimerRepository : IMonsterBossRespawnTimerRepository
{
    public Dictionary<int, DateTime> Rows { get; } = new();
    public List<(int MonsterSpawnRegionId, DateTime NextSpawnUtc)> SetCalls { get; } = [];
    public bool ThrowOnSet { get; set; }

    public ValueTask<ReadOnlyCollection<MonsterBossRespawnTimerRowDto>> GetAllAsync(CancellationToken ct)
    {
        var rows = Rows.Select(kvp => new MonsterBossRespawnTimerRowDto(kvp.Key, kvp.Value)).ToList();
        return ValueTask.FromResult(new ReadOnlyCollection<MonsterBossRespawnTimerRowDto>(rows));
    }

    public ValueTask SetAsync(int monsterSpawnRegionId, DateTime nextSpawnUtc, CancellationToken ct)
    {
        if (ThrowOnSet)
            throw new InvalidOperationException("Simulated flush failure.");

        SetCalls.Add((monsterSpawnRegionId, nextSpawnUtc));
        Rows[monsterSpawnRegionId] = nextSpawnUtc;
        return ValueTask.CompletedTask;
    }
}

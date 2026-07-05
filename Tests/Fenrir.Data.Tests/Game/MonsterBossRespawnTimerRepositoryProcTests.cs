using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.World;
using Fenrir.Data.Tests.Fixtures;
using Fenrir.Data.World;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Game;

/// <summary>
///     game.usp_MonsterBossRespawnTimer_{GetAll,Set} against real SQL Server 2025, through
///     <see cref="MonsterBossRespawnTimerRepository" /> exactly as <c>MonsterBossRespawnTracker</c> calls it.
/// </summary>
/// <remarks>
///     game.MonsterBossRespawnTimers is shared by every test in the "SqlServer" collection for the whole
///     assembly run, in no guaranteed order -- every assertion below reads back only what THIS test itself
///     just wrote (a region id unique to it), never assuming the table starts empty.
/// </remarks>
[Collection("SqlServer")]
public class MonsterBossRespawnTimerRepositoryProcTests
{
    private readonly IMonsterBossRespawnTimerRepository _repository;

    public MonsterBossRespawnTimerRepositoryProcTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder
            .Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        var db = services.BuildServiceProvider().GetRequiredService<ICaeriusNetDbContext>();
        _repository = new MonsterBossRespawnTimerRepository(db);
    }

    [Fact]
    public async Task SetAsync_ThenGetAllAsync_RoundTripsTheDeadline()
    {
        // world.MonsterSpawnRegions row 1 always exists (world.* is seeded once for the whole test database).
        const int regionId = 1;
        var deadline = new DateTime(2026, 3, 15, 12, 30, 45, 250, DateTimeKind.Utc);

        await _repository.SetAsync(regionId, deadline, CancellationToken.None);

        var rows = await _repository.GetAllAsync(CancellationToken.None);
        var row = Assert.Single(rows, r => r.MonsterSpawnRegionId == regionId);
        Assert.Equal(deadline, row.NextSpawnUtc);
    }

    [Fact]
    public async Task SetAsync_CalledTwiceForTheSameRegion_OverwritesRatherThanDuplicating()
    {
        const int regionId = 2;
        await _repository.SetAsync(regionId, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CancellationToken.None);

        var second = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        await _repository.SetAsync(regionId, second, CancellationToken.None);

        var rows = await _repository.GetAllAsync(CancellationToken.None);
        var matching = rows.Where(r => r.MonsterSpawnRegionId == regionId).ToList();
        var row = Assert.Single(matching);
        Assert.Equal(second, row.NextSpawnUtc);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsRowsForDifferentRegionsIndependently()
    {
        var first = new DateTime(2026, 2, 2, 2, 2, 2, DateTimeKind.Utc);
        var second = new DateTime(2026, 3, 3, 3, 3, 3, DateTimeKind.Utc);
        await _repository.SetAsync(3, first, CancellationToken.None);
        await _repository.SetAsync(4, second, CancellationToken.None);

        var rows = await _repository.GetAllAsync(CancellationToken.None);

        Assert.Equal(first, Assert.Single(rows, r => r.MonsterSpawnRegionId == 3).NextSpawnUtc);
        Assert.Equal(second, Assert.Single(rows, r => r.MonsterSpawnRegionId == 4).NextSpawnUtc);
    }
}

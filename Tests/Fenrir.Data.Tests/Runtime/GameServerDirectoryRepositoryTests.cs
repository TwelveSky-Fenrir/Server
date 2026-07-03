using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Runtime;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Runtime;

/// <summary>
///     Exercises <see cref="GameServerDirectoryRepository" /> against a real SQL Server 2025 instance running
///     the Database/ migrations (architecture reference §14.1). <c>GetDirectoryAsync</c> wraps its query in
///     <c>AddInMemoryCache("shards:directory", TimeSpan.FromSeconds(2))</c>, and CaeriusNet's in-memory cache
///     is process-wide static storage keyed by that string (not scoped to a single <see cref="ICaeriusNetDbContext" />
///     or test instance) -- so every test here waits comfortably past the 2 s TTL before reading back through
///     <c>GetDirectoryAsync</c>, rather than risk observing another call's (including its own prior call's)
///     cached snapshot instead of the row it just wrote.
/// </summary>
[Collection("SqlServer")]
public sealed class GameServerDirectoryRepositoryTests : IDisposable
{
    // Comfortably above the repository's 2 s cache TTL -- see class remarks.
    private static readonly TimeSpan CacheBypassDelay = TimeSpan.FromMilliseconds(2500);

    private readonly ServiceProvider _provider;
    private readonly GameServerDirectoryRepository _repository;

    public GameServerDirectoryRepositoryTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder.Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        _provider = services.BuildServiceProvider();
        _repository = new GameServerDirectoryRepository(_provider.GetRequiredService<ICaeriusNetDbContext>());
    }

    public void Dispose()
    {
        _provider.Dispose();
    }

    [Fact]
    public async Task HeartbeatAsync_InsertsANewDirectoryEntry()
    {
        const byte shardId = 250;

        await _repository.HeartbeatAsync(shardId, "shard-250.internal", 7350, 12, 500,
            4.25f, CancellationToken.None);
        await Task.Delay(CacheBypassDelay);

        var directory = await _repository.GetDirectoryAsync(CancellationToken.None);
        var entry = Assert.Single(directory, e => e.ShardId == shardId);

        Assert.Equal("shard-250.internal", entry.Host);
        Assert.Equal(7350, entry.Port);
        Assert.Equal(12, entry.Ccu);
        Assert.Equal(500, entry.Capacity);
        Assert.Equal(4.25f, entry.TickP99Ms);
    }

    [Fact]
    public async Task HeartbeatAsync_CalledTwiceForTheSameShard_UpdatesTheExistingEntryInsteadOfAddingASecond()
    {
        const byte shardId = 251;

        await _repository.HeartbeatAsync(shardId, "shard-251-old.internal", 7351, 5, 500,
            3.0f, CancellationToken.None);
        await _repository.HeartbeatAsync(shardId, "shard-251-new.internal", 7352, 40, 500,
            6.5f, CancellationToken.None);
        await Task.Delay(CacheBypassDelay);

        var directory = await _repository.GetDirectoryAsync(CancellationToken.None);
        // Assert.Single with a predicate is itself the upsert assertion: it fails if the second Heartbeat
        // inserted a duplicate row instead of overwriting the first.
        var entry = Assert.Single(directory, e => e.ShardId == shardId);

        Assert.Equal("shard-251-new.internal", entry.Host);
        Assert.Equal(7352, entry.Port);
        Assert.Equal(40, entry.Ccu);
    }
}

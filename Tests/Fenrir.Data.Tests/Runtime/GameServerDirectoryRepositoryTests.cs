using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Runtime;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Runtime;

[Collection("SqlServer")]
public sealed class GameServerDirectoryRepositoryTests : IDisposable
{
    private static readonly TimeSpan CacheBypassDelay = TimeSpan.FromMilliseconds(2500);

    private readonly ServiceProvider _provider;
    private readonly IGameServerDirectoryRepository _repository;

    public GameServerDirectoryRepositoryTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder.Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        _provider = services.BuildServiceProvider();
        _repository = new GameServerDirectoryRepository(_provider.GetRequiredService<ICaeriusNetDbContext>(),
            _provider.GetRequiredService<ICaeriusNetCache>());
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
        var entry = Assert.Single(directory, e => e.ShardId == shardId);

        Assert.Equal("shard-251-new.internal", entry.Host);
        Assert.Equal(7352, entry.Port);
        Assert.Equal(40, entry.Ccu);
    }

    [Fact]
    public async Task GetDirectoryAsync_WithNarrowerCallerSuppliedCutoff_ExcludesAnEntryStillInsideTheDefaultWindow()
    {
        const byte shardId = 252;

        await _repository.HeartbeatAsync(shardId, "shard-252.internal", 7354, 3, 500, 1.5f, CancellationToken.None);
        await Task.Delay(CacheBypassDelay);

        var withDefaultCutoff = await _repository.GetDirectoryAsync(CancellationToken.None);
        Assert.Contains(withDefaultCutoff, e => e.ShardId == shardId);

        var withNarrowCutoff = await _repository.GetDirectoryAsync(1, CancellationToken.None);
        Assert.DoesNotContain(withNarrowCutoff, e => e.ShardId == shardId);
    }
}

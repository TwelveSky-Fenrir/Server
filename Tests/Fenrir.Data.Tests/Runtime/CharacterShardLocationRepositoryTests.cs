using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Runtime;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Runtime;

[Collection("SqlServer")]
public sealed class CharacterShardLocationRepositoryTests : IDisposable
{
    private readonly IGameServerDirectoryRepository _directory;
    private readonly ServiceProvider _provider;
    private readonly ICharacterShardLocationRepository _repository;

    public CharacterShardLocationRepositoryTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder.Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        _provider = services.BuildServiceProvider();
        var db = _provider.GetRequiredService<ICaeriusNetDbContext>();
        _repository = new CharacterShardLocationRepository(db);
        _directory = new GameServerDirectoryRepository(db, _provider.GetRequiredService<ICaeriusNetCache>());
    }

    public void Dispose()
    {
        _provider.Dispose();
    }

    private Task HeartbeatAsync(byte shardId)
    {
        return _directory.HeartbeatAsync(shardId, $"shard-{shardId}.internal", 11000 + shardId, 1, 500, 1.0f,
            CancellationToken.None).AsTask();
    }

    [Fact]
    public async Task UpsertAsync_ThenFindByCharacterIdAsync_ReadsBackEveryField()
    {
        const byte shardId = 210;
        const int characterId = 900210;
        await HeartbeatAsync(shardId);

        await _repository.UpsertAsync(characterId, shardId, 42, "ShardLoc210", 2,
            CancellationToken.None);

        var row = await _repository.FindByCharacterIdAsync(characterId, CancellationToken.None);

        Assert.NotNull(row);
        Assert.Equal(characterId, row.CharacterId);
        Assert.Equal(shardId, row.ShardId);
        Assert.Equal((short)42, row.MapId);
        Assert.Equal("ShardLoc210", row.AvatarName);
        Assert.Equal((byte)2, row.Tribe);
    }

    [Fact]
    public async Task UpsertAsync_ThenFindByNameAsync_FindsTheRow()
    {
        const byte shardId = 211;
        const int characterId = 900211;
        await HeartbeatAsync(shardId);

        await _repository.UpsertAsync(characterId, shardId, 7, "ShardLoc211", 0,
            CancellationToken.None);

        var row = await _repository.FindByNameAsync("ShardLoc211", CancellationToken.None);

        Assert.NotNull(row);
        Assert.Equal(characterId, row.CharacterId);
        Assert.Equal((short)7, row.MapId);
    }

    [Fact]
    public async Task UpsertAsync_CalledTwiceForTheSameCharacter_UpdatesInsteadOfDuplicating()
    {
        const byte shardId = 212;
        const int characterId = 900212;
        await HeartbeatAsync(shardId);

        await _repository.UpsertAsync(characterId, shardId, 1, "ShardLoc212Od", 0,
            CancellationToken.None);
        await _repository.UpsertAsync(characterId, shardId, 2, "ShardLoc212Nw", 1,
            CancellationToken.None);

        var row = await _repository.FindByCharacterIdAsync(characterId, CancellationToken.None);

        Assert.NotNull(row);
        Assert.Equal((short)2, row.MapId);
        Assert.Equal("ShardLoc212Nw", row.AvatarName);
        Assert.Equal((byte)1, row.Tribe);

        Assert.Null(await _repository.FindByNameAsync("ShardLoc212Od", CancellationToken.None));
    }

    [Fact]
    public async Task RemoveAsync_ScopedToShardId_NeverClobbersARowTheCharacterAlreadyMovedAwayFrom()
    {
        const byte originalShardId = 213;
        const byte newShardId = 214;
        const int characterId = 900213;
        await HeartbeatAsync(originalShardId);
        await HeartbeatAsync(newShardId);

        await _repository.UpsertAsync(characterId, originalShardId, 1, "ShardLoc213", 0,
            CancellationToken.None);
        await _repository.UpsertAsync(characterId, newShardId, 2, "ShardLoc213", 0,
            CancellationToken.None);

        await _repository.RemoveAsync(characterId, originalShardId, CancellationToken.None);

        var row = await _repository.FindByCharacterIdAsync(characterId, CancellationToken.None);
        Assert.NotNull(row);
        Assert.Equal(newShardId, row.ShardId);
    }

    [Fact]
    public async Task RemoveAsync_MatchingShardId_DeletesTheRow()
    {
        const byte shardId = 215;
        const int characterId = 900215;
        await HeartbeatAsync(shardId);
        await _repository.UpsertAsync(characterId, shardId, 1, "ShardLoc215", 0,
            CancellationToken.None);

        await _repository.RemoveAsync(characterId, shardId, CancellationToken.None);

        Assert.Null(await _repository.FindByCharacterIdAsync(characterId, CancellationToken.None));
    }

    [Fact]
    public async Task FindByCharacterIdAsync_UnknownCharacter_ReturnsNull()
    {
        Assert.Null(await _repository.FindByCharacterIdAsync(-999999, CancellationToken.None));
    }

    [Fact]
    public async Task FindByCharacterIdAsync_OwningShardHasNeverHeartbeated_ReturnsNull()
    {
        const byte neverHeartbeatedShardId = 216;
        const int characterId = 900216;

        await _repository.UpsertAsync(characterId, neverHeartbeatedShardId, 1, "ShardLoc216",
            0, CancellationToken.None);

        Assert.Null(await _repository.FindByCharacterIdAsync(characterId, CancellationToken.None));
    }
}

using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Runtime;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Runtime;

[Collection("SqlServer")]
public sealed class GuildBuffExpiryRelayRepositoryTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IGuildBuffExpiryRelayRepository _repository;

    public GuildBuffExpiryRelayRepositoryTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder.Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        _provider = services.BuildServiceProvider();
        var db = _provider.GetRequiredService<ICaeriusNetDbContext>();
        _repository = new GuildBuffExpiryRelayRepository(db);
    }

    public void Dispose()
    {
        _provider.Dispose();
    }

    private async Task DrainAsync(byte shardId)
    {
        await _repository.PollAsync(shardId, 999_999, CancellationToken.None);
    }

    [Fact]
    public async Task PublishAsync_ThenPollAsync_FromAnotherShard_ReadsBackEveryField()
    {
        const byte sourceShardId = 1;
        const byte otherShardId = 2;
        await DrainAsync(otherShardId);

        var entry = new GuildBuffExpiryRelayEntry(sourceShardId, 4001, 0);
        await _repository.PublishAsync(entry, CancellationToken.None);
        var rows = await _repository.PollAsync(otherShardId, 999_999, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(4001, row.GuildId);
        Assert.Equal(0, row.NewBuffTime);
        Assert.True(row.RelayId > 0);
    }

    [Fact]
    public async Task PollAsync_NeverReturnsARowToItsOwnPublishingShard()
    {
        const byte sourceShardId = 3;
        await DrainAsync(sourceShardId);

        await _repository.PublishAsync(
            new GuildBuffExpiryRelayEntry(sourceShardId, 4002, -5), CancellationToken.None);

        var ownShardRows =
            await _repository.PollAsync(sourceShardId, 999_999, CancellationToken.None);
        Assert.Empty(ownShardRows);
    }

    [Fact]
    public async Task PublishAsync_FansOutToEveryOtherShard_NotJustOne()
    {
        const byte sourceShardId = 4;
        const byte firstOtherShardId = 5;
        const byte secondOtherShardId = 6;
        await DrainAsync(firstOtherShardId);
        await DrainAsync(secondOtherShardId);

        await _repository.PublishAsync(
            new GuildBuffExpiryRelayEntry(sourceShardId, 4003, 0), CancellationToken.None);

        var firstRows =
            await _repository.PollAsync(firstOtherShardId, 999_999, CancellationToken.None);
        var secondRows =
            await _repository.PollAsync(secondOtherShardId, 999_999, CancellationToken.None);

        Assert.Equal(4003, Assert.Single(firstRows).GuildId);
        Assert.Equal(4003, Assert.Single(secondRows).GuildId);
    }

    [Fact]
    public async Task PollAsync_CalledTwice_SecondCallOnlyReturnsRowsPublishedSinceTheFirstPoll()
    {
        const byte sourceShardId = 7;
        const byte pollingShardId = 8;
        await DrainAsync(pollingShardId);

        await _repository.PublishAsync(
            new GuildBuffExpiryRelayEntry(sourceShardId, 4004, 0), CancellationToken.None);

        var firstPoll =
            await _repository.PollAsync(pollingShardId, 999_999, CancellationToken.None);
        Assert.Single(firstPoll);

        var secondPollNoNewRows =
            await _repository.PollAsync(pollingShardId, 999_999, CancellationToken.None);
        Assert.Empty(secondPollNoNewRows);

        await _repository.PublishAsync(
            new GuildBuffExpiryRelayEntry(sourceShardId, 4005, 0), CancellationToken.None);

        var thirdPoll =
            await _repository.PollAsync(pollingShardId, 999_999, CancellationToken.None);
        Assert.Equal(4005, Assert.Single(thirdPoll).GuildId);
    }

    [Fact]
    public async Task PollAsync_ZeroRetention_ReapsRowsRegardlessOfWhichShardPublishedThem()
    {
        const byte pollingShardId = 9;
        const byte firstSourceShardId = 10;
        const byte secondSourceShardId = 11;
        await DrainAsync(pollingShardId);
        await DrainAsync(firstSourceShardId);

        await _repository.PublishAsync(
            new GuildBuffExpiryRelayEntry(firstSourceShardId, 4006, 0), CancellationToken.None);
        await _repository.PublishAsync(
            new GuildBuffExpiryRelayEntry(secondSourceShardId, 4007, 0), CancellationToken.None);

        var firstCallRows =
            await _repository.PollAsync(pollingShardId, 0, CancellationToken.None);
        Assert.Equal(2, firstCallRows.Length);

        var laterPollFromAnotherShard =
            await _repository.PollAsync(firstSourceShardId, 999_999, CancellationToken.None);
        Assert.Empty(laterPollFromAnotherShard);
    }

    [Fact]
    public async Task PollAsync_NothingPublishedSinceLastPoll_ReturnsEmpty()
    {
        const byte shardId = 12;

        await DrainAsync(shardId);

        var rows = await _repository.PollAsync(shardId, 999_999, CancellationToken.None);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task PublishAsync_CalledTwiceWithTheSameEntryInstance_OnlyInsertsOneRow()
    {
        const byte sourceShardId = 13;
        const byte otherShardId = 14;
        await DrainAsync(otherShardId);

        var entry = new GuildBuffExpiryRelayEntry(sourceShardId, 4008, 0);
        await _repository.PublishAsync(entry, CancellationToken.None);
        await _repository.PublishAsync(entry, CancellationToken.None);

        var rows = await _repository.PollAsync(otherShardId, 999_999, CancellationToken.None);
        Assert.Single(rows);
    }
}

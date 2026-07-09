using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Runtime;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Runtime;

/// <summary>
///     runtime.usp_RvrSiegeEventRelay_{Publish,Poll} against real SQL Server 2025, through
///     <see cref="RvrSiegeEventRelayRepository" /> exactly as <c>RvrSiegeEventRelayHost</c> calls it. Same
///     broadcast shape as GuildTribeBroadcastRelay/GuildBuffExpiryRelay (every OTHER live shard sees a
///     published row, never the publishing shard itself) -- see usp_RvrSiegeEventRelay_Poll.sql's own header
///     for the SourceShardId self-exclusion.
/// </summary>
/// <remarks>
///     Because this is a genuine broadcast, a shard id that has never polled before retroactively sees the
///     entire un-reaped backlog since RelayId 0 -- including rows left over from an earlier test method
///     sharing this same table within the run. Every test below primes ("catches up") whichever shard id(s) it
///     polls via <see cref="DrainAsync" /> before publishing its own row, so each test's assertions only ever
///     see rows it published itself, independent of test execution order or what ran before it.
/// </remarks>
[Collection("SqlServer")]
public sealed class RvrSiegeEventRelayRepositoryTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IRvrSiegeEventRelayRepository _repository;

    public RvrSiegeEventRelayRepositoryTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder.Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        _provider = services.BuildServiceProvider();
        var db = _provider.GetRequiredService<ICaeriusNetDbContext>();
        _repository = new RvrSiegeEventRelayRepository(db);
    }

    public void Dispose()
    {
        _provider.Dispose();
    }

    private static byte[] Payload(int firstField)
    {
        var data = new byte[130];
        BitConverter.GetBytes(firstField).CopyTo(data, 0);
        return data;
    }

    private async Task DrainAsync(byte shardId)
    {
        await _repository.PollAsync(shardId, retentionSeconds: 999_999, CancellationToken.None);
    }

    [Fact]
    public async Task PublishAsync_ThenPollAsync_FromAnotherShard_ReadsBackSortAndDataByteForByte()
    {
        const byte sourceShardId = 1;
        const byte otherShardId = 2;
        await DrainAsync(otherShardId);

        var entry = new RvrSiegeEventRelayEntry(sourceShardId, Sort: 40, Data: Payload(0));
        await _repository.PublishAsync(entry, CancellationToken.None);
        var rows = await _repository.PollAsync(otherShardId, retentionSeconds: 999_999, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal(40, row.Sort);
        Assert.Equal(entry.Data, row.Data);
        Assert.True(row.RelayId > 0);
    }

    [Fact]
    public async Task PollAsync_NeverReturnsARowToItsOwnPublishingShard()
    {
        const byte sourceShardId = 3;
        await DrainAsync(sourceShardId);

        await _repository.PublishAsync(new RvrSiegeEventRelayEntry(sourceShardId, Sort: 2, Data: Payload(4)),
            CancellationToken.None);

        // The originating shard already applied and broadcast locally at publish time (in-process, never
        // through this repository) -- usp_RvrSiegeEventRelay_Poll excludes SourceShardId = @ShardId so it is
        // never re-delivered to itself via the cross-shard path.
        var ownShardRows =
            await _repository.PollAsync(sourceShardId, retentionSeconds: 999_999, CancellationToken.None);
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

        await _repository.PublishAsync(new RvrSiegeEventRelayEntry(sourceShardId, Sort: 46, Data: Payload(1)),
            CancellationToken.None);

        var firstRows =
            await _repository.PollAsync(firstOtherShardId, retentionSeconds: 999_999, CancellationToken.None);
        var secondRows =
            await _repository.PollAsync(secondOtherShardId, retentionSeconds: 999_999, CancellationToken.None);

        Assert.Equal(46, Assert.Single(firstRows).Sort);
        Assert.Equal(46, Assert.Single(secondRows).Sort);
    }

    [Fact]
    public async Task PollAsync_CalledTwice_SecondCallOnlyReturnsRowsPublishedSinceTheFirstPoll()
    {
        const byte sourceShardId = 7;
        const byte pollingShardId = 8;
        await DrainAsync(pollingShardId);

        await _repository.PublishAsync(new RvrSiegeEventRelayEntry(sourceShardId, Sort: 38, Data: Payload(1)),
            CancellationToken.None);

        var firstPoll = await _repository.PollAsync(pollingShardId, retentionSeconds: 999_999, CancellationToken.None);
        Assert.Single(firstPoll);

        var secondPollNoNewRows =
            await _repository.PollAsync(pollingShardId, retentionSeconds: 999_999, CancellationToken.None);
        Assert.Empty(secondPollNoNewRows);

        await _repository.PublishAsync(new RvrSiegeEventRelayEntry(sourceShardId, Sort: 45, Data: Payload(0)),
            CancellationToken.None);

        var thirdPoll = await _repository.PollAsync(pollingShardId, retentionSeconds: 999_999, CancellationToken.None);
        Assert.Equal(45, Assert.Single(thirdPoll).Sort);
    }

    [Fact]
    public async Task PollAsync_ZeroRetention_ReapsRowsRegardlessOfWhichShardPublishedThem()
    {
        const byte pollingShardId = 9;
        const byte firstSourceShardId = 10;
        const byte secondSourceShardId = 11;
        await DrainAsync(pollingShardId);
        await DrainAsync(firstSourceShardId);

        await _repository.PublishAsync(new RvrSiegeEventRelayEntry(firstSourceShardId, Sort: 9, Data: Payload(2)),
            CancellationToken.None);
        await _repository.PublishAsync(new RvrSiegeEventRelayEntry(secondSourceShardId, Sort: 9, Data: Payload(3)),
            CancellationToken.None);

        var firstCallRows = await _repository.PollAsync(pollingShardId, retentionSeconds: 0, CancellationToken.None);
        Assert.Equal(2, firstCallRows.Length);

        var laterPollFromAnotherShard =
            await _repository.PollAsync(firstSourceShardId, retentionSeconds: 999_999, CancellationToken.None);
        Assert.Empty(laterPollFromAnotherShard);
    }
}

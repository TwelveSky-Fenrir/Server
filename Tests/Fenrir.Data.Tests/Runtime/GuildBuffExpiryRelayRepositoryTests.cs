using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Runtime;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Runtime;

/// <summary>
///     runtime.usp_GuildBuffExpiryRelay_{Publish,Poll} against real SQL Server 2025, through
///     <see cref="GuildBuffExpiryRelayRepository" /> exactly as (the future) <c>GuildBuffExpiryRelayHost</c>
///     calls it. Same broadcast shape as GuildTribeBroadcastRelay (every OTHER live shard sees a published row,
///     never the publishing shard itself) rather than SocialCrossShardRelay's point-to-point addressing --
///     see usp_GuildBuffExpiryRelay_Poll.sql's own header for the SourceShardId self-exclusion.
/// </summary>
/// <remarks>
///     Because this is a genuine broadcast (unlike point-to-point SocialCrossShardRelay), a shard id that has
///     never polled before retroactively sees the entire un-reaped backlog since RelayId 0 -- including rows
///     left over from an earlier test method sharing this same table within the run. Every test below primes
///     ("catches up") whichever shard id(s) it polls via <see cref="DrainAsync" /> before publishing its own
///     row, so each test's assertions only ever see rows it published itself, independent of test execution
///     order or what ran before it.
/// </remarks>
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

    /// <summary>Advances <paramref name="shardId" />'s own cursor past whatever is already in the table, without
    /// reaping anything (a large retention), so a subsequent poll only returns rows published after this call.</summary>
    private async Task DrainAsync(byte shardId)
    {
        await _repository.PollAsync(shardId, retentionSeconds: 999_999, CancellationToken.None);
    }

    [Fact]
    public async Task PublishAsync_ThenPollAsync_FromAnotherShard_ReadsBackEveryField()
    {
        const byte sourceShardId = 1;
        const byte otherShardId = 2;
        await DrainAsync(otherShardId);

        var entry = new GuildBuffExpiryRelayEntry(sourceShardId, GuildId: 4001, NewBuffTime: 0);
        await _repository.PublishAsync(entry, CancellationToken.None);
        var rows = await _repository.PollAsync(otherShardId, retentionSeconds: 999_999, CancellationToken.None);

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
            new GuildBuffExpiryRelayEntry(sourceShardId, GuildId: 4002, NewBuffTime: -5), CancellationToken.None);

        // The originating shard already delivered locally at publish time (in-process, never through this
        // repository) -- usp_GuildBuffExpiryRelay_Poll excludes SourceShardId = @ShardId so it is never
        // re-delivered to itself via the cross-shard path.
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

        await _repository.PublishAsync(
            new GuildBuffExpiryRelayEntry(sourceShardId, GuildId: 4003, NewBuffTime: 0), CancellationToken.None);

        // Unlike SocialCrossShardRelay's point-to-point addressing, this is a genuine cluster-wide broadcast:
        // every other live shard's own next poll sees the same row, independently.
        var firstRows =
            await _repository.PollAsync(firstOtherShardId, retentionSeconds: 999_999, CancellationToken.None);
        var secondRows =
            await _repository.PollAsync(secondOtherShardId, retentionSeconds: 999_999, CancellationToken.None);

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
            new GuildBuffExpiryRelayEntry(sourceShardId, GuildId: 4004, NewBuffTime: 0), CancellationToken.None);

        var firstPoll =
            await _repository.PollAsync(pollingShardId, retentionSeconds: 999_999, CancellationToken.None);
        Assert.Single(firstPoll);

        // Cursor already advanced past the first row -- an immediate re-poll with nothing new published sees
        // nothing, it is never re-delivered.
        var secondPollNoNewRows =
            await _repository.PollAsync(pollingShardId, retentionSeconds: 999_999, CancellationToken.None);
        Assert.Empty(secondPollNoNewRows);

        await _repository.PublishAsync(
            new GuildBuffExpiryRelayEntry(sourceShardId, GuildId: 4005, NewBuffTime: 0), CancellationToken.None);

        var thirdPoll =
            await _repository.PollAsync(pollingShardId, retentionSeconds: 999_999, CancellationToken.None);
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
            new GuildBuffExpiryRelayEntry(firstSourceShardId, GuildId: 4006, NewBuffTime: 0), CancellationToken.None);
        await _repository.PublishAsync(
            new GuildBuffExpiryRelayEntry(secondSourceShardId, GuildId: 4007, NewBuffTime: 0), CancellationToken.None);

        // The SELECT happens before the reap DELETE, so this call still returns both rows even though
        // @RetentionSeconds = 0 makes every already-inserted row (regardless of SourceShardId) immediately
        // reap-eligible.
        var firstCallRows =
            await _repository.PollAsync(pollingShardId, retentionSeconds: 0, CancellationToken.None);
        Assert.Equal(2, firstCallRows.Length);

        // Reap is purely time-based, not scoped to the polling shard's own filter -- a later poll from a
        // shard that never even saw @RetentionSeconds = 0 itself still finds the rows gone (its cursor was
        // already caught up by the DrainAsync priming call above, so it isn't retroactively catching up on
        // unrelated backlog either).
        var laterPollFromAnotherShard =
            await _repository.PollAsync(firstSourceShardId, retentionSeconds: 999_999, CancellationToken.None);
        Assert.Empty(laterPollFromAnotherShard);
    }

    [Fact]
    public async Task PollAsync_NothingPublishedSinceLastPoll_ReturnsEmpty()
    {
        const byte shardId = 12;

        // First poll catches this shard up past whatever backlog already exists in the shared table (from
        // other test methods run earlier in this run) -- exercises the same "brand new shard's first poll"
        // path a real shard hits on its very first cycle after boot, just not asserted on directly since the
        // backlog size is not under this test's control.
        await DrainAsync(shardId);

        // With nothing published since that catch-up poll, an immediate re-poll sees nothing.
        var rows = await _repository.PollAsync(shardId, retentionSeconds: 999_999, CancellationToken.None);
        Assert.Empty(rows);
    }
}

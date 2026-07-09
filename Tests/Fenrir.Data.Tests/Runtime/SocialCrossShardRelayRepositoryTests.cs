using CaeriusNet.Abstractions;
using CaeriusNet.Builders;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Runtime;
using Fenrir.Data.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Fenrir.Data.Tests.Runtime;

/// <summary>
///     runtime.usp_SocialCrossShardRelay_{Publish,Poll} against real SQL Server 2025, through
///     <see cref="SocialCrossShardRelayRepository" /> exactly as the (future) <c>SocialCrossShardRelayHost</c>
///     calls it. Unlike <see cref="CharacterShardLocationRepositoryTests" />, no
///     <c>runtime.GameServerDirectory</c> heartbeat is required first -- neither proc joins against it.
/// </summary>
[Collection("SqlServer")]
public sealed class SocialCrossShardRelayRepositoryTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly ISocialCrossShardRelayRepository _repository;

    public SocialCrossShardRelayRepositoryTests(SqlServerFixture fixture)
    {
        var services = CaeriusNetBuilder.Create(new ServiceCollection())
            .WithSqlServer(fixture.ConnectionString)
            .Build();

        _provider = services.BuildServiceProvider();
        var db = _provider.GetRequiredService<ICaeriusNetDbContext>();
        _repository = new SocialCrossShardRelayRepository(db);
    }

    public void Dispose()
    {
        _provider.Dispose();
    }

    private static SocialCrossShardRelayEntry MakeAsk(byte sourceShardId, int sourceCharacterId,
        string sourceAvatarName, byte targetShardId, int targetCharacterId,
        SocialCrossShardRelayKind kind = SocialCrossShardRelayKind.Friend)
    {
        return new SocialCrossShardRelayEntry(kind, SocialCrossShardRelayMessageType.Ask, null, null,
            sourceShardId, sourceCharacterId, sourceAvatarName, targetShardId, targetCharacterId, null);
    }

    private static SocialCrossShardRelayEntry MakeAnswer(byte sourceShardId, int sourceCharacterId,
        string sourceAvatarName, byte targetShardId, int targetCharacterId, bool accepted, byte? reasonCode,
        long askRelayId, SocialCrossShardRelayKind kind = SocialCrossShardRelayKind.Friend)
    {
        return new SocialCrossShardRelayEntry(kind, SocialCrossShardRelayMessageType.Answer, accepted, reasonCode,
            sourceShardId, sourceCharacterId, sourceAvatarName, targetShardId, targetCharacterId, askRelayId);
    }

    [Fact]
    public async Task PublishAsync_ThenPollAsync_ReadsBackEveryFieldOfAnAskRow()
    {
        const byte targetShardId = 220;
        var entry = MakeAsk(7, 200, "AskerName",
            targetShardId, 100, SocialCrossShardRelayKind.Duel);

        await _repository.PublishAsync(entry, CancellationToken.None);
        var rows = await _repository.PollAsync(targetShardId, 999_999, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal((byte)SocialCrossShardRelayKind.Duel, row.Kind);
        Assert.Equal((byte)SocialCrossShardRelayMessageType.Ask, row.MessageType);
        Assert.Null(row.Accepted);
        Assert.Null(row.ReasonCode);
        Assert.Equal((byte)7, row.SourceShardId);
        Assert.Equal(200, row.SourceCharacterId);
        Assert.Equal("AskerName", row.SourceAvatarName);
        Assert.Equal(targetShardId, row.TargetShardId);
        Assert.Equal(100, row.TargetCharacterId);
        Assert.Null(row.AskRelayId);
        Assert.True(row.RelayId > 0);
    }

    [Fact]
    public async Task PublishAsync_ThenPollAsync_ReadsBackEveryFieldOfAnAnswerRow()
    {
        const byte targetShardId = 221;
        var ask = MakeAsk(7, 200, "Asker",
            targetShardId, 100);
        await _repository.PublishAsync(ask, CancellationToken.None);
        var askRow = Assert.Single(
            await _repository.PollAsync(targetShardId, 999_999, CancellationToken.None));

        // Answer is addressed back the other way: Answer.TargetShardId is the original asker's shard.
        var answer = MakeAnswer(targetShardId, 100, "Target",
            7, 200, false, 5, askRow.RelayId);
        await _repository.PublishAsync(answer, CancellationToken.None);

        var answerRow = Assert.Single(
            await _repository.PollAsync(7, 999_999, CancellationToken.None));

        Assert.Equal((byte)SocialCrossShardRelayMessageType.Answer, answerRow.MessageType);
        Assert.False(answerRow.Accepted);
        Assert.Equal((byte)5, answerRow.ReasonCode);
        Assert.Equal(targetShardId, answerRow.SourceShardId);
        Assert.Equal(100, answerRow.SourceCharacterId);
        Assert.Equal("Target", answerRow.SourceAvatarName);
        Assert.Equal((byte)7, answerRow.TargetShardId);
        Assert.Equal(200, answerRow.TargetCharacterId);
        Assert.Equal(askRow.RelayId, answerRow.AskRelayId);
    }

    [Fact]
    public async Task PollAsync_PointToPoint_OnlyTheNamedTargetShardEverSeesTheRow()
    {
        const byte targetShardId = 230;
        const byte unrelatedShardId = 231;
        await _repository.PublishAsync(
            MakeAsk(7, 200, "Asker",
                targetShardId, 100),
            CancellationToken.None);

        // Unlike GuildTribeBroadcastRelay's fan-out (every OTHER shard sees it), a shard not named as the
        // target never sees this row at all -- not even once.
        var unrelatedShardRows =
            await _repository.PollAsync(unrelatedShardId, 999_999, CancellationToken.None);
        Assert.Empty(unrelatedShardRows);

        var targetShardRows =
            await _repository.PollAsync(targetShardId, 999_999, CancellationToken.None);
        Assert.Single(targetShardRows);
    }

    [Fact]
    public async Task PollAsync_CalledTwice_SecondCallOnlyReturnsRowsPublishedSinceTheFirstPoll()
    {
        const byte targetShardId = 240;
        await _repository.PublishAsync(
            MakeAsk(7, 200, "First",
                targetShardId, 100),
            CancellationToken.None);

        var firstPoll = await _repository.PollAsync(targetShardId, 999_999, CancellationToken.None);
        Assert.Single(firstPoll);

        // Cursor already advanced past the first row -- an immediate re-poll with nothing new published sees
        // nothing, it is never re-delivered.
        var secondPollNoNewRows =
            await _repository.PollAsync(targetShardId, 999_999, CancellationToken.None);
        Assert.Empty(secondPollNoNewRows);

        await _repository.PublishAsync(
            MakeAsk(7, 201, "Second",
                targetShardId, 100),
            CancellationToken.None);

        var thirdPoll = await _repository.PollAsync(targetShardId, 999_999, CancellationToken.None);
        var onlyNewRow = Assert.Single(thirdPoll);
        Assert.Equal("Second", onlyNewRow.SourceAvatarName);
    }

    [Fact]
    public async Task PollAsync_ZeroRetention_ReapsRowsRegardlessOfWhichShardTheyWereAddressedTo()
    {
        const byte pollingShardId = 250;
        const byte otherTargetShardId = 251;
        await _repository.PublishAsync(
            MakeAsk(7, 200, "ForPollingShard",
                pollingShardId, 100),
            CancellationToken.None);
        await _repository.PublishAsync(
            MakeAsk(7, 201, "ForOtherShard",
                otherTargetShardId, 101),
            CancellationToken.None);

        // The SELECT happens before the reap DELETE, so this call still returns its own row even though
        // @RetentionSeconds = 0 makes every already-inserted row (any target shard) immediately reap-eligible.
        var pollingShardRows =
            await _repository.PollAsync(pollingShardId, 0, CancellationToken.None);
        Assert.Single(pollingShardRows);

        // The row addressed to otherTargetShardId was reaped as a side effect of the call above, even though
        // that shard never polled with a small retention itself -- reap is purely time-based, not scoped to
        // the polling shard's own TargetShardId filter.
        var otherShardRows =
            await _repository.PollAsync(otherTargetShardId, 999_999, CancellationToken.None);
        Assert.Empty(otherShardRows);
    }

    [Fact]
    public async Task PollAsync_ShardNeverPublishedTo_ReturnsEmpty()
    {
        var rows = await _repository.PollAsync(252, 999_999, CancellationToken.None);
        Assert.Empty(rows);
    }
}

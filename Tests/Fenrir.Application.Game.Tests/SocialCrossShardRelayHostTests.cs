using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Hosting;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests;

// WS1.4 cross-shard social-negotiation relay poll loop -- point-to-point sibling of
// GuildTribeBroadcastRelayHost. Constructed directly with fakes (no DI container), same idiom as
// AccountSessionKickPollHostTests.
public class SocialCrossShardRelayHostTests
{
    private const byte ShardId = 3;

    [Fact]
    public async Task PollOnceAsync_QueuedEntry_IsPublishedToTheRepository()
    {
        var relay = new FakeSocialCrossShardRelayRepository();
        var host = CreateHost(relay, []);

        var entry = new SocialCrossShardRelayEntry(SocialCrossShardRelayKind.Duel,
            SocialCrossShardRelayMessageType.Ask, null, null, ShardId, 100, "Asker", 7, 200, null);
        Assert.True(host.Enqueue(entry));

        await host.PollOnceAsync(CancellationToken.None);

        var published = Assert.Single(relay.Published);
        Assert.Equal(entry, published);
    }

    [Fact]
    public async Task PollOnceAsync_DeliveredAsk_IsRoutedToTheMatchingKindHandler_AndNoOtherHandler()
    {
        var relay = new FakeSocialCrossShardRelayRepository();
        var duelHandler = new FakeSocialCrossShardRelayHandler(SocialCrossShardRelayKind.Duel);
        var friendHandler = new FakeSocialCrossShardRelayHandler(SocialCrossShardRelayKind.Friend);
        var host = CreateHost(relay, [duelHandler, friendHandler]);

        var dto = new SocialCrossShardRelayDto(RelayId: 55, Kind: (byte)SocialCrossShardRelayKind.Duel,
            MessageType: (byte)SocialCrossShardRelayMessageType.Ask, Accepted: null, ReasonCode: null,
            SourceShardId: 7, SourceCharacterId: 200, SourceAvatarName: "Challenger", TargetShardId: ShardId,
            TargetCharacterId: 100, AskRelayId: null);
        relay.NextPoll = [dto];

        await host.PollOnceAsync(CancellationToken.None);

        var handled = Assert.Single(duelHandler.AsksHandled);
        Assert.Equal(55, handled.RelayId);
        Assert.Empty(duelHandler.AnswersHandled);
        Assert.Empty(friendHandler.AsksHandled);
        Assert.Empty(friendHandler.AnswersHandled);
    }

    [Fact]
    public async Task PollOnceAsync_DeliveredAnswer_IsRoutedToHandleAnswerAsync_NotHandleAskAsync()
    {
        var relay = new FakeSocialCrossShardRelayRepository();
        var tradeHandler = new FakeSocialCrossShardRelayHandler(SocialCrossShardRelayKind.Trade);
        var host = CreateHost(relay, [tradeHandler]);

        var dto = new SocialCrossShardRelayDto(RelayId: 9, Kind: (byte)SocialCrossShardRelayKind.Trade,
            MessageType: (byte)SocialCrossShardRelayMessageType.Answer, Accepted: true, ReasonCode: null,
            SourceShardId: 7, SourceCharacterId: 200, SourceAvatarName: "Target", TargetShardId: ShardId,
            TargetCharacterId: 100, AskRelayId: 3);
        relay.NextPoll = [dto];

        await host.PollOnceAsync(CancellationToken.None);

        var handled = Assert.Single(tradeHandler.AnswersHandled);
        Assert.Equal(3, handled.AskRelayId);
        Assert.Empty(tradeHandler.AsksHandled);
    }

    [Fact]
    public async Task PollOnceAsync_KindWithNoRegisteredHandler_IsDroppedWithoutThrowing()
    {
        var relay = new FakeSocialCrossShardRelayRepository();
        var host = CreateHost(relay, []);

        var dto = new SocialCrossShardRelayDto(RelayId: 1, Kind: (byte)SocialCrossShardRelayKind.Mentor,
            MessageType: (byte)SocialCrossShardRelayMessageType.Ask, Accepted: null, ReasonCode: null,
            SourceShardId: 7, SourceCharacterId: 200, SourceAvatarName: "Asker", TargetShardId: ShardId,
            TargetCharacterId: 100, AskRelayId: null);
        relay.NextPoll = [dto];

        await Record.ExceptionAsync(() => host.PollOnceAsync(CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task PollOnceAsync_PublishFailure_IsIsolated_DoesNotThrow()
    {
        var relay = new FakeSocialCrossShardRelayRepository { ThrowOnPublish = new InvalidOperationException("boom") };
        var host = CreateHost(relay, []);

        host.Enqueue(new SocialCrossShardRelayEntry(SocialCrossShardRelayKind.Party,
            SocialCrossShardRelayMessageType.Ask, null, null, ShardId, 1, "A", 2, 3, null));

        var ex = await Record.ExceptionAsync(() => host.PollOnceAsync(CancellationToken.None).AsTask());
        Assert.Null(ex);
    }

    private static SocialCrossShardRelayHost CreateHost(FakeSocialCrossShardRelayRepository relay,
        IEnumerable<ISocialCrossShardRelayHandler> handlers)
    {
        return new SocialCrossShardRelayHost(handlers, relay,
            Options.Create(new GameServerOptions { ShardId = ShardId }),
            NullLogger<SocialCrossShardRelayHost>.Instance);
    }
}

using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Hosting;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Chat;

// WS-C3 cross-shard whisper (op39 / SECRET_CHAT) relay poll loop -- point-to-point sibling of
// SocialCrossShardRelayHost, minus its Ask/Answer negotiation (a whisper is fire-and-forget). Delivery is done
// by the host itself (like GuildTribeBroadcastRelayHost), not routed to a per-Kind handler.
public class ChatCrossShardRelayHostTests
{
    private const byte ShardId = 3;
    private const short MapId = 1;

    private static readonly ItemLinkInfo EmptyLink =
        new() { Index = 0, Activity = 0, Value = 0, Socket = new int[3] };

    [Fact]
    public async Task PollOnceAsync_QueuedWhisper_IsPublishedToTheRepository()
    {
        var relay = new FakeChatCrossShardRelayRepository();
        var host = CreateHost(ZoneTestKit.CreateRegistry(), relay);

        var entry = new ChatCrossShardWhisperEntry(ShardId, 100, "Asker", 7, 200, "Target", "hi there", 0);
        Assert.True(host.Enqueue(entry));

        await host.PollOnceAsync(CancellationToken.None);

        var published = Assert.Single(relay.Published);
        Assert.Equal(entry, published);
    }

    [Fact]
    public async Task PollOnceAsync_InboundWhisper_ForLocallyPresentTarget_IsDeliveredAsResult3()
    {
        var relay = new FakeChatCrossShardRelayRepository();
        var zones = ZoneTestKit.CreateRegistry(new GameServerOptions { ShardId = ShardId });
        zones.Initialize([MapId]);
        zones.TryGet(MapId, out var zone);
        var (session, pipe) = ZoneTestKit.CreateSession(200);
        zone!.Post(ZoneCommand.Enter(200, ZoneTestKit.EnterData(session, MapId, "Target")));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe); // clear the enter broadcast before asserting the whisper

        relay.NextPoll =
        [
            new ChatCrossShardWhisperDto(1, 5, 100, "Asker", ShardId, 200, "Target", "hello there", 1)
        ];

        var host = CreateHost(zones, relay);
        await host.PollOnceAsync(CancellationToken.None);

        await PacketAssert.AssertSentAsync(pipe, new WhisperResponse
        {
            Result = 3,
            ZoneNumber = 0,
            AvatarName = "Asker",
            Content = "hello there",
            AuthType = 1,
            Link = EmptyLink
        });
    }

    [Fact]
    public async Task PollOnceAsync_InboundWhisper_ForTargetNotOnThisShard_IsCleanDropped()
    {
        var relay = new FakeChatCrossShardRelayRepository();
        var zones = ZoneTestKit.CreateRegistry(new GameServerOptions { ShardId = ShardId });
        zones.Initialize([MapId]);
        zones.TryGet(MapId, out var zone);
        var (session, pipe) = ZoneTestKit.CreateSession(200);
        zone!.Post(ZoneCommand.Enter(200, ZoneTestKit.EnterData(session, MapId, "Target")));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        // Addressed to a character id that is not present on this shard -- the since-departed-target parity case.
        relay.NextPoll =
        [
            new ChatCrossShardWhisperDto(1, 5, 100, "Asker", ShardId, 999, "Ghost", "hello there", 0)
        ];

        var host = CreateHost(zones, relay);
        var ex = await Record.ExceptionAsync(() => host.PollOnceAsync(CancellationToken.None).AsTask());

        Assert.Null(ex);
        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task PollOnceAsync_InboundWhisper_ForTargetMidZoneTransfer_IsCleanDropped()
    {
        var relay = new FakeChatCrossShardRelayRepository();
        var zones = ZoneTestKit.CreateRegistry(new GameServerOptions { ShardId = ShardId });
        zones.Initialize([MapId]);
        zones.TryGet(MapId, out var zone);
        var (session, pipe) = ZoneTestKit.CreateSession(200);
        zone!.Post(ZoneCommand.Enter(200, ZoneTestKit.EnterData(session, MapId, "Target")));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        ZoneTestKit.DrainOutbound(pipe);

        Assert.True(zone.TryGetPlayer(200, out var target));
        target!.IsMovingZone = true; // flagged mid-transfer -- skipped by the same guard even if still registered

        relay.NextPoll =
        [
            new ChatCrossShardWhisperDto(1, 5, 100, "Asker", ShardId, 200, "Target", "hello there", 0)
        ];

        var host = CreateHost(zones, relay);
        await host.PollOnceAsync(CancellationToken.None);

        PacketAssert.AssertNothingSent(pipe);
    }

    [Fact]
    public async Task PollOnceAsync_PublishFailure_IsIsolated_DoesNotThrow()
    {
        var relay = new FakeChatCrossShardRelayRepository
            { ThrowOnPublish = new InvalidOperationException("boom") };
        var host = CreateHost(ZoneTestKit.CreateRegistry(), relay);

        host.Enqueue(new ChatCrossShardWhisperEntry(ShardId, 1, "A", 2, 3, "B", "hi", 0));

        var ex = await Record.ExceptionAsync(() => host.PollOnceAsync(CancellationToken.None).AsTask());
        Assert.Null(ex);
    }

    private static ChatCrossShardRelayHost CreateHost(ZoneRegistry zones, FakeChatCrossShardRelayRepository relay)
    {
        return new ChatCrossShardRelayHost(zones, relay,
            Options.Create(new GameServerOptions { ShardId = ShardId }),
            NullLogger<ChatCrossShardRelayHost>.Instance);
    }
}

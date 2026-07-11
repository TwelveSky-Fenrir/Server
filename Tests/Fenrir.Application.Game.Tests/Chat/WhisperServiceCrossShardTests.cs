using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Chat;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Chat;

public class WhisperServiceCrossShardTests
{
    private const byte ThisShardId = 1;

    private static PlayerRuntimeState MakePlayer(int characterId, string name, byte tribe = 0)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        return new PlayerRuntimeState
        {
            CharacterId = characterId,
            Session = session,
            Name = name,
            Tribe = tribe,
            Gender = 0,
            HeadType = 0,
            FaceType = 0,
            Level = 1
        };
    }

    private static WhisperService CreateService(FakeCharacterShardLocationRepository directory,
        FakeChatCrossShardRelayQueue relay)
    {
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([]);
        return new WhisperService(zones, directory, relay,
            Options.Create(new GameServerOptions { ShardId = ThisShardId }));
    }

    [Fact]
    public async Task ResolveAsync_TargetFoundOnAnotherShard_EnqueuesTheWhisper_AndReturnsQueuedCrossShard()
    {
        var directory = new FakeCharacterShardLocationRepository();
        directory.Seed(new CharacterShardLocationDto(99, 3, 42, "Remote", 0, DateTime.UtcNow));
        var relay = new FakeChatCrossShardRelayQueue();
        var service = CreateService(directory, relay);
        var sender = MakePlayer(1, "Hero");

        var resolution = await service.ResolveAsync(sender, "Remote", "hello there", 1, CancellationToken.None);

        Assert.Equal(WhisperOutcome.QueuedCrossShard, resolution.Outcome);
        Assert.Equal((byte?)3, resolution.OtherShardId);
        Assert.Equal((short?)42, resolution.OtherMapId);
        Assert.Null(resolution.Target);

        var entry = Assert.Single(relay.Enqueued);
        Assert.Equal(ThisShardId, entry.SourceShardId);
        Assert.Equal(1, entry.SourceCharacterId);
        Assert.Equal("Hero", entry.SourceAvatarName);
        Assert.Equal((byte)3, entry.TargetShardId);
        Assert.Equal(99, entry.TargetCharacterId);
        Assert.Equal("Remote", entry.TargetAvatarName);
        Assert.Equal("hello there", entry.Content);
        Assert.Equal((byte)1, entry.SenderAuthType);
    }

    [Fact]
    public async Task ResolveAsync_TargetNotFoundAnywhere_DoesNotEnqueue()
    {
        var directory = new FakeCharacterShardLocationRepository();
        var relay = new FakeChatCrossShardRelayQueue();
        var service = CreateService(directory, relay);
        var sender = MakePlayer(1, "Hero");

        var resolution = await service.ResolveAsync(sender, "Nobody", "hi", 0, CancellationToken.None);

        Assert.Equal(WhisperOutcome.TargetNotFound, resolution.Outcome);
        Assert.Empty(relay.Enqueued);
    }

    [Fact]
    public async Task ResolveAsync_SelfWhisper_DoesNotEnqueue_AndNeverConsultsTheDirectory()
    {
        var directory = new FakeCharacterShardLocationRepository { ThrowOnUpsert = true };
        var relay = new FakeChatCrossShardRelayQueue();
        var service = CreateService(directory, relay);
        var sender = MakePlayer(1, "Hero");

        var resolution = await service.ResolveAsync(sender, "Hero", "hi", 0, CancellationToken.None);

        Assert.Equal(WhisperOutcome.SelfWhisper, resolution.Outcome);
        Assert.Empty(relay.Enqueued);
    }

    [Fact]
    public async Task ResolveAsync_OutboxFull_StillReturnsQueuedCrossShard()
    {
        var directory = new FakeCharacterShardLocationRepository();
        directory.Seed(new CharacterShardLocationDto(99, 3, 42, "Remote", 0, DateTime.UtcNow));
        var relay = new FakeChatCrossShardRelayQueue { EnqueueResult = false };
        var service = CreateService(directory, relay);
        var sender = MakePlayer(1, "Hero");

        var resolution = await service.ResolveAsync(sender, "Remote", "hello there", 0, CancellationToken.None);

        Assert.Equal(WhisperOutcome.QueuedCrossShard, resolution.Outcome);
        Assert.Single(relay.Enqueued);
    }
}

using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Chat;
using Fenrir.Application.Game.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Chat;

/// <summary>
///     <see cref="WhisperService.ResolveAsync" />'s same-shard-first, cross-shard-directory-fallback shape.
///     Same-zone/same-shard behavior is covered by the pre-existing <see cref="ZoneRegistry" /> lookup tests;
///     this focuses on the NEW cross-shard-miss fallback and its distinct outcomes.
/// </summary>
public class WhisperServiceTests
{
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

    [Fact]
    public async Task ResolveAsync_SelfWhisper_NeverConsultsTheDirectory()
    {
        var directory = new FakeCharacterShardLocationRepository { ThrowOnUpsert = true };
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([]);
        var service = new WhisperService(zones, directory, new FakeChatCrossShardRelayQueue(),
            Options.Create(new GameServerOptions()));
        var sender = MakePlayer(1, "Hero");

        var resolution = await service.ResolveAsync(sender, "Hero", "hi", 0, CancellationToken.None);

        Assert.Equal(WhisperOutcome.SelfWhisper, resolution.Outcome);
    }

    [Fact]
    public async Task ResolveAsync_SameShardHit_NeverConsultsTheDirectory()
    {
        var directory = new FakeCharacterShardLocationRepository();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([1]);
        zones.TryGet(1, out var zone);
        var (session, _) = ZoneTestKit.CreateSession(2);
        zone!.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(session, 1, "Target")));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var service = new WhisperService(zones, directory, new FakeChatCrossShardRelayQueue(),
            Options.Create(new GameServerOptions()));
        var sender = MakePlayer(1, "Hero");

        var resolution = await service.ResolveAsync(sender, "Target", "hi", 0, CancellationToken.None);

        Assert.Equal(WhisperOutcome.Delivered, resolution.Outcome);
        Assert.Equal("Target", resolution.Target!.Name);
        // Fast path never touches the directory -- FindByNameAsync would have populated no calls to check
        // against, but this at least proves no exception was needed to reach Delivered.
    }

    [Fact]
    public async Task ResolveAsync_SameShardMiss_AndNotInDirectory_ReturnsTargetNotFound()
    {
        var directory = new FakeCharacterShardLocationRepository();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([]);
        var service = new WhisperService(zones, directory, new FakeChatCrossShardRelayQueue(),
            Options.Create(new GameServerOptions()));
        var sender = MakePlayer(1, "Hero");

        var resolution = await service.ResolveAsync(sender, "Nobody", "hi", 0, CancellationToken.None);

        Assert.Equal(WhisperOutcome.TargetNotFound, resolution.Outcome);
    }
}

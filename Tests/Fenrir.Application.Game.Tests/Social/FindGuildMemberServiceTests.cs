using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Social;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.Social;

public class FindGuildMemberServiceTests
{
    private static PlayerRuntimeState MakePlayer(int characterId, string name, int? guildId)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        return new PlayerRuntimeState
        {
            CharacterId = characterId,
            Session = session,
            Name = name,
            Tribe = 0,
            Gender = 0,
            HeadType = 0,
            FaceType = 0,
            Level = 1,
            GuildId = guildId
        };
    }

    [Fact]
    public async Task FindZoneAsync_AskerHasNoGuild_ReturnsHasGuildFalse_NeverConsultsTheDirectory()
    {
        var directory = new FakeCharacterShardLocationRepository { ThrowOnUpsert = true };
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([]);
        var service = new FindGuildMemberService(zones, directory, NullLogger<FindGuildMemberService>.Instance);
        var asker = MakePlayer(1, "Asker", null);

        var result = await service.FindZoneAsync(asker, "Anyone", CancellationToken.None);

        Assert.False(result.HasGuild);
        Assert.Equal(-1, result.ZoneNumber);
    }

    [Fact]
    public async Task FindZoneAsync_SameShardHit_ReturnsFoundZoneNumber()
    {
        var directory = new FakeCharacterShardLocationRepository();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([6]);
        zones.TryGet(6, out var zone);
        var (session, _) = ZoneTestKit.CreateSession(2);
        zone!.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(session, 6, "Target")));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var service = new FindGuildMemberService(zones, directory, NullLogger<FindGuildMemberService>.Instance);
        var asker = MakePlayer(1, "Asker", 10);

        var result = await service.FindZoneAsync(asker, "Target", CancellationToken.None);

        Assert.True(result.HasGuild);
        Assert.Equal(6, result.ZoneNumber);
    }

    [Fact]
    public async Task FindZoneAsync_SameShardMiss_FallsBackToDirectory_ReturnsItsMapId()
    {
        var directory = new FakeCharacterShardLocationRepository();
        directory.Seed(new CharacterShardLocationDto(2, 4, 88, "Target", 0,
            DateTime.UtcNow));
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([]);
        var service = new FindGuildMemberService(zones, directory, NullLogger<FindGuildMemberService>.Instance);
        var asker = MakePlayer(1, "Asker", 10);

        var result = await service.FindZoneAsync(asker, "Target", CancellationToken.None);

        Assert.True(result.HasGuild);
        Assert.Equal(88, result.ZoneNumber);
    }

    [Fact]
    public async Task FindZoneAsync_SameShardMiss_AndNotInDirectory_ReturnsNegativeOneZoneNumber()
    {
        var directory = new FakeCharacterShardLocationRepository();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([]);
        var service = new FindGuildMemberService(zones, directory, NullLogger<FindGuildMemberService>.Instance);
        var asker = MakePlayer(1, "Asker", 10);

        var result = await service.FindZoneAsync(asker, "Nobody", CancellationToken.None);

        Assert.True(result.HasGuild);
        Assert.Equal(-1, result.ZoneNumber);
    }
}

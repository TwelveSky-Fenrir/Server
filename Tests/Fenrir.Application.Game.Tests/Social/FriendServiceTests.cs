using System.Collections.ObjectModel;
using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Social;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Abstractions.Social;

namespace Fenrir.Application.Game.Tests.Social;

/// <summary>
///     <see cref="FriendService.LocateAsync" />'s same-shard-first, cross-shard-directory-fallback shape,
///     including re-applying the same-tribe gate against the directory's own denormalized Tribe column.
/// </summary>
public class FriendServiceTests
{
    private static PlayerRuntimeState MakePlayer(int characterId, string name, byte tribe)
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

    private static FriendService CreateService(ZoneRegistry zones, ICharacterShardLocationRepository directory)
    {
        return new FriendService(zones, new FriendRegistry(), new ThrowingFriendRepository(), directory);
    }

    [Fact]
    public async Task LocateAsync_IndexOutOfRange_NeverConsultsTheDirectory()
    {
        var directory = new FakeCharacterShardLocationRepository { ThrowOnUpsert = true };
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([]);
        var service = CreateService(zones, directory);
        var asker = MakePlayer(1, "Asker", tribe: 0);

        var result = await service.LocateAsync(asker, 99, CancellationToken.None);

        Assert.Equal(FriendLocateResultKind.IndexOutOfRange, result.Kind);
    }

    [Fact]
    public async Task LocateAsync_SlotEmpty_NeverConsultsTheDirectory()
    {
        var directory = new FakeCharacterShardLocationRepository();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([]);
        var service = CreateService(zones, directory);
        var asker = MakePlayer(1, "Asker", tribe: 0);

        var result = await service.LocateAsync(asker, 0, CancellationToken.None);

        Assert.Equal(FriendLocateResultKind.SlotEmpty, result.Kind);
    }

    [Fact]
    public async Task LocateAsync_SameShardHit_SameTribe_ReturnsFriendsMapId()
    {
        var directory = new FakeCharacterShardLocationRepository();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([5]);
        zones.TryGet(5, out var zone);
        var (session, _) = ZoneTestKit.CreateSession(2);
        zone!.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(session, 5, "Friend", tribe: 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var service = CreateService(zones, directory);
        var asker = MakePlayer(1, "Asker", tribe: 1);
        asker.Friends[0] = 2;

        var result = await service.LocateAsync(asker, 0, CancellationToken.None);

        Assert.Equal(FriendLocateResultKind.Found, result.Kind);
        Assert.Equal(5, result.ZoneNumber);
    }

    [Fact]
    public async Task LocateAsync_SameShardHit_DifferentTribe_ReturnsFoundWithNegativeOneZoneNumber()
    {
        var directory = new FakeCharacterShardLocationRepository();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([5]);
        zones.TryGet(5, out var zone);
        var (session, _) = ZoneTestKit.CreateSession(2);
        zone!.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(session, 5, "Friend", tribe: 2)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        var service = CreateService(zones, directory);
        var asker = MakePlayer(1, "Asker", tribe: 1);
        asker.Friends[0] = 2;

        var result = await service.LocateAsync(asker, 0, CancellationToken.None);

        Assert.Equal(FriendLocateResultKind.Found, result.Kind);
        Assert.Equal(-1, result.ZoneNumber);
    }

    [Fact]
    public async Task LocateAsync_SameShardMiss_FallsBackToDirectory_SameTribe_ReturnsMapId()
    {
        var directory = new FakeCharacterShardLocationRepository();
        directory.Seed(new CharacterShardLocationDto(2, ShardId: 9, MapId: 77, AvatarName: "Friend", Tribe: 1,
            LastSeenUtc: DateTime.UtcNow));
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([]);
        var service = CreateService(zones, directory);
        var asker = MakePlayer(1, "Asker", tribe: 1);
        asker.Friends[0] = 2;

        var result = await service.LocateAsync(asker, 0, CancellationToken.None);

        Assert.Equal(FriendLocateResultKind.Found, result.Kind);
        Assert.Equal(77, result.ZoneNumber);
    }

    [Fact]
    public async Task LocateAsync_SameShardMiss_FallsBackToDirectory_DifferentTribe_ReturnsNegativeOneWithoutASecondQuery()
    {
        var directory = new FakeCharacterShardLocationRepository();
        directory.Seed(new CharacterShardLocationDto(2, ShardId: 9, MapId: 77, AvatarName: "Friend", Tribe: 3,
            LastSeenUtc: DateTime.UtcNow));
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([]);
        var service = CreateService(zones, directory);
        var asker = MakePlayer(1, "Asker", tribe: 1);
        asker.Friends[0] = 2;

        var result = await service.LocateAsync(asker, 0, CancellationToken.None);

        Assert.Equal(FriendLocateResultKind.Found, result.Kind);
        Assert.Equal(-1, result.ZoneNumber);
    }

    [Fact]
    public async Task LocateAsync_SameShardMiss_AndNotInDirectory_ReturnsFoundWithNegativeOneZoneNumber()
    {
        var directory = new FakeCharacterShardLocationRepository();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([]);
        var service = CreateService(zones, directory);
        var asker = MakePlayer(1, "Asker", tribe: 1);
        asker.Friends[0] = 2;

        var result = await service.LocateAsync(asker, 0, CancellationToken.None);

        Assert.Equal(FriendLocateResultKind.Found, result.Kind);
        Assert.Equal(-1, result.ZoneNumber);
    }

    private sealed class ThrowingFriendRepository : IFriendRepository
    {
        public ValueTask<ReadOnlyCollection<CharacterFriendDto>> GetByCharacterAsync(int characterId,
            CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask AddAsync(int characterId, byte slot, int friendCharacterId, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public ValueTask RemoveAsync(int characterId, byte slot, CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }
}

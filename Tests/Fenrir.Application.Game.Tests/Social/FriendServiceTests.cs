using System.Collections.ObjectModel;
using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Services.Social;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Data.Abstractions.Social;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Tests.Social;

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
        return CreateService(zones, new FriendRegistry(), directory);
    }

    private static FriendService CreateService(ZoneRegistry zones, FriendRegistry friends,
        ICharacterShardLocationRepository directory)
    {
        return new FriendService(zones, friends, new DuelRegistry(), new TradeRegistry(), new PartyRegistry(),
            new GuildInviteRegistry(), new MentorRegistry(), new ThrowingFriendRepository(), directory,
            new FakeSocialCrossShardRelayQueue(), Options.Create(new GameServerOptions()),
            new CapturingLogger<FriendService>());
    }

    [Fact]
    public async Task Ask_AskerBusy_AndTargetNameDoesNotExist_ReturnsAskerBusy_NotTargetNotFound()
    {
        var friends = new FriendRegistry();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([1]);
        zones.TryGet(1, out var zone);

        var (askerSession, _) = ZoneTestKit.CreateSession(1);
        zone!.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(askerSession, 1, "Asker", tribe: 1)));
        var (pendingSession, _) = ZoneTestKit.CreateSession(2);
        zone.Post(ZoneCommand.Enter(2, ZoneTestKit.EnterData(pendingSession, 1, "PendingTarget", tribe: 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));

        Assert.True(zone.TryGetPlayer(1, out var asker));
        Assert.Equal(FriendAskOutcome.Sent, friends.TryAsk(1, 2));

        var service = CreateService(zones, friends, new FakeCharacterShardLocationRepository());

        var result = await service.AskAsync(zone, asker!, "NoSuchAvatar", CancellationToken.None);

        Assert.Equal(FriendAskResultKind.AskerBusy, result);
    }

    [Fact]
    public async Task Ask_SameShardMiss_ResolvesCrossShard_PublishesAskAndReturnsSentCrossShard()
    {
        var friends = new FriendRegistry();
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([1]);
        zones.TryGet(1, out var zone);
        var (askerSession, _) = ZoneTestKit.CreateSession(1);
        zone!.Post(ZoneCommand.Enter(1, ZoneTestKit.EnterData(askerSession, 1, "Asker", tribe: 1)));
        zone.Tick(TimeSpan.FromMilliseconds(50));
        Assert.True(zone.TryGetPlayer(1, out var asker));

        var directory = new FakeCharacterShardLocationRepository();
        directory.Seed(new CharacterShardLocationDto(2, 9, 77, "RemoteFriend", 1, DateTime.UtcNow));

        var relay = new FakeSocialCrossShardRelayQueue();
        var service = new FriendService(zones, friends, new DuelRegistry(), new TradeRegistry(), new PartyRegistry(),
            new GuildInviteRegistry(), new MentorRegistry(), new ThrowingFriendRepository(), directory, relay,
            Options.Create(new GameServerOptions { ShardId = 1 }), new CapturingLogger<FriendService>());

        var result = await service.AskAsync(zone, asker!, "RemoteFriend", CancellationToken.None);

        Assert.Equal(FriendAskResultKind.SentCrossShard, result);
        Assert.Single(relay.Enqueued);
        var entry = relay.Enqueued[0];
        Assert.Equal(SocialCrossShardRelayKind.Friend, entry.Kind);
        Assert.Equal(SocialCrossShardRelayMessageType.Ask, entry.MessageType);
        Assert.Equal(1, entry.SourceCharacterId);
        Assert.Equal((byte)9, entry.TargetShardId);
        Assert.Equal(2, entry.TargetCharacterId);
        Assert.True(friends.IsNegotiating(1));
    }

    [Fact]
    public async Task LocateAsync_IndexOutOfRange_NeverConsultsTheDirectory()
    {
        var directory = new FakeCharacterShardLocationRepository { ThrowOnUpsert = true };
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([]);
        var service = CreateService(zones, directory);
        var asker = MakePlayer(1, "Asker", 0);

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
        var asker = MakePlayer(1, "Asker", 0);

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
        var asker = MakePlayer(1, "Asker", 1);
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
        var asker = MakePlayer(1, "Asker", 1);
        asker.Friends[0] = 2;

        var result = await service.LocateAsync(asker, 0, CancellationToken.None);

        Assert.Equal(FriendLocateResultKind.Found, result.Kind);
        Assert.Equal(-1, result.ZoneNumber);
    }

    [Fact]
    public async Task LocateAsync_SameShardMiss_FallsBackToDirectory_SameTribe_ReturnsMapId()
    {
        var directory = new FakeCharacterShardLocationRepository();
        directory.Seed(new CharacterShardLocationDto(2, 9, 77, "Friend", 1,
            DateTime.UtcNow));
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([]);
        var service = CreateService(zones, directory);
        var asker = MakePlayer(1, "Asker", 1);
        asker.Friends[0] = 2;

        var result = await service.LocateAsync(asker, 0, CancellationToken.None);

        Assert.Equal(FriendLocateResultKind.Found, result.Kind);
        Assert.Equal(77, result.ZoneNumber);
    }

    [Fact]
    public async Task
        LocateAsync_SameShardMiss_FallsBackToDirectory_DifferentTribe_ReturnsNegativeOneWithoutASecondQuery()
    {
        var directory = new FakeCharacterShardLocationRepository();
        directory.Seed(new CharacterShardLocationDto(2, 9, 77, "Friend", 3,
            DateTime.UtcNow));
        var zones = ZoneTestKit.CreateRegistry();
        zones.Initialize([]);
        var service = CreateService(zones, directory);
        var asker = MakePlayer(1, "Asker", 1);
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
        var asker = MakePlayer(1, "Asker", 1);
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

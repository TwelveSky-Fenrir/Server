using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Social;

public class CommunityWorkGateTests
{
    private const int PlayerId = 10;
    private const int OtherId = 20;

    private static PlayerRuntimeState MakePlayer(int characterId, bool pshopOpen = false)
    {
        var (session, _) = ZoneTestKit.CreateSession(characterId);
        return new PlayerRuntimeState
        {
            CharacterId = characterId,
            Session = session,
            Name = $"Player{characterId}",
            Tribe = 0,
            Gender = 0,
            HeadType = 0,
            FaceType = 0,
            Level = 1,
            PshopOpen = pshopOpen
        };
    }

    private static (DuelRegistry Duels, TradeRegistry Trades, FriendRegistry Friends, PartyRegistry Parties,
        MentorRegistry Mentors, GuildInviteRegistry GuildInvites) MakeRegistries()
    {
        return (new DuelRegistry(), new TradeRegistry(), new FriendRegistry(), new PartyRegistry(),
            new MentorRegistry(), new GuildInviteRegistry());
    }

    [Fact]
    public void IsBusy_NoFlagsSet_ReturnsFalse()
    {
        var player = MakePlayer(PlayerId);
        var (duels, trades, friends, parties, mentors, guildInvites) = MakeRegistries();

        Assert.False(CommunityWorkGate.IsBusy(player, duels, trades, friends, parties, mentors, guildInvites));
    }

    [Fact]
    public void IsBusy_PersonalShopOpen_ReturnsTrue()
    {
        var player = MakePlayer(PlayerId, pshopOpen: true);
        var (duels, trades, friends, parties, mentors, guildInvites) = MakeRegistries();

        Assert.True(CommunityWorkGate.IsBusy(player, duels, trades, friends, parties, mentors, guildInvites));
    }

    [Fact]
    public void IsBusy_DuelNegotiating_ReturnsTrue()
    {
        var player = MakePlayer(PlayerId);
        var (duels, trades, friends, parties, mentors, guildInvites) = MakeRegistries();

        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(PlayerId, OtherId, false));

        Assert.True(CommunityWorkGate.IsBusy(player, duels, trades, friends, parties, mentors, guildInvites));
    }

    [Fact]
    public void IsBusy_TradeBusy_ReturnsTrue()
    {
        var player = MakePlayer(PlayerId);
        var (duels, trades, friends, parties, mentors, guildInvites) = MakeRegistries();

        Assert.Equal(TradeAskOutcome.Sent, trades.TryAsk(PlayerId, OtherId));

        Assert.True(CommunityWorkGate.IsBusy(player, duels, trades, friends, parties, mentors, guildInvites));
    }

    [Fact]
    public void IsBusy_FriendNegotiating_ReturnsTrue()
    {
        var player = MakePlayer(PlayerId);
        var (duels, trades, friends, parties, mentors, guildInvites) = MakeRegistries();

        Assert.Equal(FriendAskOutcome.Sent, friends.TryAsk(PlayerId, OtherId));

        Assert.True(CommunityWorkGate.IsBusy(player, duels, trades, friends, parties, mentors, guildInvites));
    }

    [Fact]
    public void IsBusy_PartyNegotiating_ReturnsTrue()
    {
        var player = MakePlayer(PlayerId);
        var (duels, trades, friends, parties, mentors, guildInvites) = MakeRegistries();

        Assert.Equal(PartyInviteOutcome.Sent, parties.TryInvite(PlayerId, 1, 0, OtherId, 1, 0));

        Assert.True(CommunityWorkGate.IsBusy(player, duels, trades, friends, parties, mentors, guildInvites));
    }

    [Fact]
    public void IsBusy_MentorNegotiating_ReturnsTrue()
    {
        var player = MakePlayer(PlayerId);
        var (duels, trades, friends, parties, mentors, guildInvites) = MakeRegistries();

        Assert.Equal(MentorAskOutcome.Sent, mentors.TryAsk(PlayerId, OtherId, false, false));

        Assert.True(CommunityWorkGate.IsBusy(player, duels, trades, friends, parties, mentors, guildInvites));
    }

    [Fact]
    public void IsBusy_GuildInviteNegotiating_ReturnsTrue()
    {
        var player = MakePlayer(PlayerId);
        var (duels, trades, friends, parties, mentors, guildInvites) = MakeRegistries();

        Assert.Equal(GuildInviteAskOutcome.Sent, guildInvites.TryAsk(PlayerId, OtherId));

        Assert.True(CommunityWorkGate.IsBusy(player, duels, trades, friends, parties, mentors, guildInvites));
    }

    [Fact]
    public void IsBusy_ActivelyDueling_ReturnsTrue()
    {
        var player = MakePlayer(PlayerId);
        var (duels, trades, friends, parties, mentors, guildInvites) = MakeRegistries();

        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(PlayerId, OtherId, false));
        Assert.True(duels.TryAnswer(OtherId, true, out var challengerId));
        Assert.Equal(PlayerId, challengerId);
        Assert.True(duels.TryStart(PlayerId, out _));

        Assert.False(duels.IsNegotiating(PlayerId));
        Assert.True(duels.IsActivelyDueling(PlayerId));

        Assert.True(CommunityWorkGate.IsBusy(player, duels, trades, friends, parties, mentors, guildInvites));
    }

    [Fact]
    public void IsBusy_TargetSideActivelyDueling_AlsoReturnsTrueForTarget()
    {
        var target = MakePlayer(OtherId);
        var (duels, trades, friends, parties, mentors, guildInvites) = MakeRegistries();

        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(PlayerId, OtherId, false));
        Assert.True(duels.TryAnswer(OtherId, true, out _));
        Assert.True(duels.TryStart(PlayerId, out _));

        Assert.True(CommunityWorkGate.IsBusy(target, duels, trades, friends, parties, mentors, guildInvites));
    }

    [Fact]
    public void IsBusy_TargetSideNegotiating_AlsoReturnsTrueForTarget()
    {
        var target = MakePlayer(OtherId);
        var (duels, trades, friends, parties, mentors, guildInvites) = MakeRegistries();

        Assert.Equal(TradeAskOutcome.Sent, trades.TryAsk(OtherId, 30));

        Assert.True(CommunityWorkGate.IsBusy(target, duels, trades, friends, parties, mentors, guildInvites));
    }

    [Fact]
    public void IsBusy_UnrelatedCharacterBusyElsewhere_DoesNotAffectThisPlayer()
    {
        var player = MakePlayer(PlayerId);
        var (duels, trades, friends, parties, mentors, guildInvites) = MakeRegistries();

        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(30, 40, false));

        Assert.False(CommunityWorkGate.IsBusy(player, duels, trades, friends, parties, mentors, guildInvites));
    }
}

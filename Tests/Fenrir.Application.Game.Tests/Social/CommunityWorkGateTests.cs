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

/// <summary>
///     Covers <see cref="CommunityWorkGate.IsBusy" />'s 7-flag OR: personal-shop-open plus the 6 per-family
///     registries' own <c>IsNegotiating</c>/<c>IsBusy</c> state, matching legacy <c>CheckCommunityWork</c>
///     (Server/ts25zone/S07_MyGame04.cpp:185-216).
/// </summary>
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
    public void IsBusy_TargetSideNegotiating_AlsoReturnsTrueForTarget()
    {
        // The SAME registries are shared across both the requester's and the target's own IsBusy evaluation --
        // an ask target who is already mid-negotiation elsewhere is excluded too, matching legacy applying
        // CheckCommunityWork to both sides of every *_ASK_SEND handler.
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

        // Some other pair of characters negotiating a duel must not leak into PlayerId's own busy check.
        Assert.Equal(DuelAskOutcome.Sent, duels.TryAsk(30, 40, false));

        Assert.False(CommunityWorkGate.IsBusy(player, duels, trades, friends, parties, mentors, guildInvites));
    }
}

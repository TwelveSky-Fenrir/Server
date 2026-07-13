using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Social;

public static class CommunityWorkGate
{
    public static bool IsBusy(PlayerRuntimeState player, DuelRegistry duels, TradeRegistry trades,
        FriendRegistry friends, PartyRegistry parties, MentorRegistry mentors, GuildInviteRegistry guildInvites)
    {
        return player.PshopOpen
               || duels.IsNegotiating(player.CharacterId)
               || duels.IsActivelyDueling(player.CharacterId)
               || trades.IsBusy(player.CharacterId)
               || friends.IsNegotiating(player.CharacterId)
               || parties.IsNegotiating(player.CharacterId)
               || mentors.IsNegotiating(player.CharacterId)
               || guildInvites.IsNegotiating(player.CharacterId);
    }
}

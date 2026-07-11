using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Domain.Social;

/// <summary>
///     Unified <c>CheckCommunityWork()</c> gate: true if the player currently occupies ANY of the 7
///     mutually-exclusive interaction states legacy tracks on one shared field -- personal-shop-open, duel
///     negotiation, trade negotiation, friend negotiation, party negotiation, teacher/mentor negotiation, or
///     guild-invite negotiation. Every one of the 6 *_ASK_SEND request families (duel, trade, friend, teacher,
///     party, guild) is meant to consult this exact same combined check for BOTH the requester and the
///     resolved target before allowing a new interaction to begin.
///     <para>
///         Legacy enforces this via one shared field with a different tSort value per interaction kind; Fenrir
///         instead models each kind as its own independent per-family registry (<see cref="DuelRegistry" />,
///         <see cref="TradeRegistry" />, <see cref="FriendRegistry" />, <see cref="PartyRegistry" />,
///         <see cref="MentorRegistry" />, <see cref="GuildInviteRegistry" />), so this type's only job is to OR
///         all 7 flags together -- including the caller's OWN family. This intentionally replaces four
///         near-identical, previously-duplicated private helpers
///         (<c>TradeInviteService.IsExcludedByCommunityWorkOrStunDeath</c>,
///         <c>GuildInviteService.IsExcludedByCommunityWorkOrStunDeath</c>,
///         <c>FriendService.IsExcludedByCommunityWork</c>, <c>MentorAskService.IsExcludedByCommunityWork</c>)
///         that each skipped their own family's flag as an optimization -- harmless (that family's own
///         <c>TryAsk</c>/<c>TryInvite</c> already re-checks its own flag atomically at registration time,
///         immediately after this pre-check runs) but redundant to keep reimplementing per file. Checking all 7
///         unconditionally here is both simpler and a more literal match for legacy's single shared field.
///     </para>
///     <para>
///         Deliberately excludes the independent stun/post-death action-state gate
///         (<see cref="PlayerRuntimeState.IsStunned" />/<see cref="PlayerRuntimeState.IsDead" />) -- per the C21
///         behavior contract, that is a separate legacy check (Server/ts25zone/S07_MyGame04.cpp:438-459,
///         1617-1658), cited independently of <c>CheckCommunityWork</c> itself (:185-216). Two of the four
///         helpers this type replaces additionally folded that stun/death gate into their own private method;
///         callers that need both must compose them explicitly at the call site (
///         <c>
///             CommunityWorkGate.IsBusy(...)
///             || player.IsStunned || player.IsDead
///         </c>
///         ), not inside this type.
///     </para>
///     <para>
///         Personal shop is the only one of the 7 states with no matching *_ASK_SEND handler of its own (see the
///         C21 contract's Edge cases H) -- it still participates here as a pure read of
///         <see cref="PlayerRuntimeState.PshopOpen" />, since nothing in this family opens/closes a personal
///         shop itself.
///     </para>
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame04.cpp:185-216 (<c>CheckCommunityWork</c>, true if any of
///     private-shop/duel/trade/friend/party/guild/teacher state is non-zero) ; Server/ts25zone/S04_MyWork02.cpp:
///     8264,8292 (duel ask/target), :8459,8486 (trade), :9088,9113 (friend), :9311,9341 (teacher/mentor),
///     :9577,9615 (party), :9835,9865 (guild) -- the 12 requester+target call sites the contract cites.
///     <para>
///         Behavior contract: C21 (Commerce/social edges, mini-games &amp; misc guards), section H. Per that
///         contract's own Output/Side-effects sections, the specific accept/reject response each of the 6
///         calling handlers builds from this predicate's result is explicitly out of scope here ("outside this
///         contract's citations") -- this type models only the pure 7-flag predicate itself.
///     </para>
/// </remarks>
public static class CommunityWorkGate
{
    /// <summary>
    ///     True if <paramref name="player" /> is currently excluded from starting or receiving a new
    ///     interaction ask by any of the 7 <c>CheckCommunityWork</c> flags.
    /// </summary>
    public static bool IsBusy(PlayerRuntimeState player, DuelRegistry duels, TradeRegistry trades,
        FriendRegistry friends, PartyRegistry parties, MentorRegistry mentors, GuildInviteRegistry guildInvites)
    {
        return player.PshopOpen
               || duels.IsNegotiating(player.CharacterId)
               || trades.IsBusy(player.CharacterId)
               || friends.IsNegotiating(player.CharacterId)
               || parties.IsNegotiating(player.CharacterId)
               || mentors.IsNegotiating(player.CharacterId)
               || guildInvites.IsNegotiating(player.CharacterId);
    }
}

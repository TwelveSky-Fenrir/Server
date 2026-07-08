using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

/// <summary>
///     The displayed level sent to the target on a successful ask is <see cref="PlayerRuntimeState.CombinedLevel" />
///     (aLevel1+aLevel2), per the trade-ask displayed-level behavior contract -- a direct sum with no
///     additional offset/clamp at this site.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:8456-8517 (TradeInvite preconditions) ;
///     Server/ts25zone/S07_MyGame04.cpp:185-216 (<c>CheckCommunityWork</c>'s shared busy-flag check, true if
///     any of private-shop, duel, trade, friend, party, guild, or teacher state is non-zero). <see cref="Invite" />
///     composes that check from this process's own sibling negotiation registries (Duel, Friend, Party,
///     Mentor, Guild) and the stun/death action-state gate (<see cref="PlayerRuntimeState.IsStunned" />/
///     <see cref="PlayerRuntimeState.IsDead" />, Server/ts25zone/S07_MyGame04.cpp:438-459,1617-1658), mirroring
///     <c>GuildInviteService.IsExcludedByCommunityWorkOrStunDeath</c> with Guild and Trade's own-family roles
///     swapped -- this family's own trade-negotiation flag stays checked separately, and atomically, by
///     <see cref="TradeRegistry.IsBusy" />/<see cref="TradeRegistry.TryAsk" />, exactly as it did before this
///     cross-family composition was added.
///     <para>
///         The contract's second TradeInvite precondition -- the caller's avatar-action state must not be one
///         of two specific "locked" action codes -- has no modeled equivalent here: the exact two
///         <see cref="PlayerRuntimeState.ActionSort" /> values this precondition refers to were not identified
///         by the behavior contract (Server/ts25zone/S04_MyWork02.cpp:8456-8517) and are left as an open,
///         explicitly-flagged gap pending a legacy-behavior-translator/cpp-zone-gameplay-analyst re-derivation,
///         rather than guessed at.
///     </para>
/// </remarks>
public sealed class TradeInviteService(
    TradeRegistry trades,
    DuelRegistry duels,
    FriendRegistry friends,
    PartyRegistry parties,
    MentorRegistry mentors,
    GuildInviteRegistry guildInvites,
    ILogger<TradeInviteService> logger) : ITradeInviteService
{
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:8259-8277,8459-8471,9088-9101,9311-9324 (the shared
    ///     CZ_DUEL_ASK_SEND/CZ_FRIEND_ASK_SEND/CZ_PARTY_ASK_SEND/CZ_TEACHER_ASK_SEND/CZ_TRADE_ASK_SEND
    ///     pre-check family) -- legacy checks the requester's OWN busy/pose state before it ever resolves
    ///     the target avatar by name, so a busy asker naming a nonexistent/offline target gets the busy
    ///     reply, not "target not found". <see cref="TradeRegistry.IsBusy" /> and
    ///     <see cref="IsExcludedByCommunityWorkOrStunDeath" /> are therefore both checked ahead of the by-name
    ///     target lookup below; the same <see cref="TradeRegistry.IsBusy" /> check inside
    ///     <see cref="TradeRegistry.TryAsk" /> stays in place for the actual registration.
    ///     <para>
    ///         Trade-ask displayed-level combined-level extension: Server/ts25zone/S04_MyWork02.cpp:8456-8517
    ///         (full CZ_TRADE_ASK_SEND handler) ; Server/ts25zone/S04_MyWork02.cpp:8514 (the exact outward
    ///         value -- asker's ordinary level plus high level, sent verbatim with no offset) ;
    ///         Server/ts25zone/S04_MyWork02.cpp:8478-8485 (the three designated inter-tribe zone numbers and
    ///         the tribe/alliance disconnect check, confirmed unaffected by this).
    ///     </para>
    /// </remarks>
    public TradeInviteResult Invite(Zone zone, PlayerRuntimeState asker, string targetAvatarName)
    {
        if (trades.IsBusy(asker.CharacterId) || IsExcludedByCommunityWorkOrStunDeath(asker))
        {
            logger.LogDebug("Trade invite rejected: character {AskerCharacterId} is busy", asker.CharacterId);
            return new TradeInviteResult(TradeInviteResultKind.AskerBusy);
        }

        PlayerRuntimeState? target = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, targetAvatarName, StringComparison.OrdinalIgnoreCase))
            {
                target = candidate;
                break;
            }

        if (target is null)
        {
            logger.LogDebug(
                "Trade invite rejected: character {AskerCharacterId} target {TargetAvatarName} not found in map {MapId}",
                asker.CharacterId, targetAvatarName, zone.MapId);
            return new TradeInviteResult(TradeInviteResultKind.TargetNotFound);
        }

        var interTribeAllowed = zone.MapId is 37 or 119 or 124;
        if (!interTribeAllowed && asker.Tribe != target.Tribe)
        {
            logger.LogWarning(
                "Trade invite rejected: character {AskerCharacterId} (tribe {AskerTribe}) targeted cross-tribe character {TargetCharacterId} (tribe {TargetTribe}) in map {MapId} -- session will be disconnected",
                asker.CharacterId, asker.Tribe, target.CharacterId, target.Tribe, zone.MapId);
            return new TradeInviteResult(TradeInviteResultKind.MustDisconnect);
        }

        if (IsExcludedByCommunityWorkOrStunDeath(target))
        {
            logger.LogDebug("Trade invite rejected: target character {TargetCharacterId} is busy",
                target.CharacterId);
            return new TradeInviteResult(TradeInviteResultKind.TargetBusy);
        }

        switch (trades.TryAsk(asker.CharacterId, target.CharacterId))
        {
            case TradeAskOutcome.AskerBusy:
                logger.LogDebug("Trade invite rejected: character {AskerCharacterId} is busy", asker.CharacterId);
                return new TradeInviteResult(TradeInviteResultKind.AskerBusy);
            case TradeAskOutcome.TargetBusy:
                logger.LogDebug("Trade invite rejected: target character {TargetCharacterId} is busy",
                    target.CharacterId);
                return new TradeInviteResult(TradeInviteResultKind.TargetBusy);
            default:
                logger.LogDebug(
                    "Trade invite sent: character {AskerCharacterId} ({AskerName}) -> character {TargetCharacterId} ({TargetName})",
                    asker.CharacterId, asker.Name, target.CharacterId, target.Name);
                return new TradeInviteResult(TradeInviteResultKind.Sent, target.CharacterId, target.Name, asker.Name,
                    asker.CombinedLevel);
        }
    }

    /// <summary>
    ///     <c>CheckCommunityWork()</c>'s six OTHER exclusivity flags (personal shop, duel, friend, party,
    ///     guild, mentor/teacher negotiation -- this family's own trade-negotiation flag is checked
    ///     separately, and atomically, by <see cref="TradeRegistry.IsBusy" />/<see cref="TradeRegistry.TryAsk" />),
    ///     plus the independent stun/post-death action-state gate. Any one of these being true excludes
    ///     <paramref name="player" /> from starting or receiving a trade ask, exactly as it does for every
    ///     other ASK family in this codebase (mirrors <c>GuildInviteService.IsExcludedByCommunityWorkOrStunDeath</c>).
    /// </summary>
    private bool IsExcludedByCommunityWorkOrStunDeath(PlayerRuntimeState player)
    {
        return player.PshopOpen
               || duels.IsNegotiating(player.CharacterId)
               || friends.IsNegotiating(player.CharacterId)
               || parties.IsNegotiating(player.CharacterId)
               || mentors.IsNegotiating(player.CharacterId)
               || guildInvites.IsNegotiating(player.CharacterId)
               || player.IsStunned
               || player.IsDead;
    }
}

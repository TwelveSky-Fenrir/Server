using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Services.Social;

/// <inheritdoc cref="IGuildInviteService" />
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:9827-9884 (GUILD_ASK_SEND) and
///     Server/ts25zone/S07_MyGame04.cpp:185-216 (<c>CheckCommunityWork</c>'s seven-flag exclusivity check).
///     <see cref="Ask" /> composes that check from this process's own sibling negotiation registries (Duel,
///     Trade, Friend, Party, Mentor, plus this family's own <see cref="GuildInviteRegistry" />) and the
///     stun/death action-state gate (<see cref="PlayerRuntimeState.IsStunned" />/<see cref="PlayerRuntimeState.IsDead" />,
///     Server/ts25zone/S07_MyGame04.cpp:438-459,1617-1658) -- both applied to the asker before the target is
///     even resolved, and again to the target once found, matching the legacy check order. The target's own
///     "currently mid zone-transfer" gate (legacy <c>IsMovingZone()</c>) is NOT modeled: no Fenrir zone-transfer
///     state has an equivalent "pending" flag on <see cref="PlayerRuntimeState" /> today (same gap
///     <c>RankBuffHandler</c> documents for its own unrelated check) -- left as an open, explicitly-flagged gap
///     rather than guessed at.
/// </remarks>
public sealed class GuildInviteService(
    ZoneRegistry zones,
    GuildInviteRegistry invites,
    DuelRegistry duels,
    TradeRegistry trades,
    FriendRegistry friends,
    PartyRegistry parties,
    MentorRegistry mentors) : IGuildInviteService
{
    public GuildInviteAskResultKind Ask(Zone zone, PlayerRuntimeState asker, string targetAvatarName)
    {
        if (asker.GuildId is null || !GuildRoleCodec.IsMasterOrSubMaster(asker.GuildRoleDb))
            return GuildInviteAskResultKind.NotAuthorized;

        if (IsExcludedByCommunityWorkOrStunDeath(asker))
            return GuildInviteAskResultKind.AskerBusy;

        var target = FindPlayerByName(zone, targetAvatarName);
        if (target is null)
            return GuildInviteAskResultKind.TargetNotFound;

        if (target.GuildId is not null)
            return GuildInviteAskResultKind.TargetAlreadyGuilded;

        if (asker.Tribe != target.Tribe)
            return GuildInviteAskResultKind.TribeMismatch;

        if (IsExcludedByCommunityWorkOrStunDeath(target))
            return GuildInviteAskResultKind.TargetBusy;

        switch (invites.TryAsk(asker.CharacterId, target.CharacterId))
        {
            case GuildInviteAskOutcome.AskerBusy:
                return GuildInviteAskResultKind.AskerBusy;
            case GuildInviteAskOutcome.TargetBusy:
                return GuildInviteAskResultKind.TargetBusy;
            case GuildInviteAskOutcome.Sent:
                target.Session.Send(new GuildInviteResponse { AvatarName = asker.Name });
                return GuildInviteAskResultKind.Sent;
            default:
                return GuildInviteAskResultKind.AskerBusy;
        }
    }

    public void Answer(int targetId, int answerCode)
    {
        if (!invites.TryAnswer(targetId, answerCode == 0, out var askerId))
            return;

        if (zones.TryGetPlayer(askerId, out var asker))
            asker.Session.Send(new GuildInviteAnswerResponse { Answer = answerCode });
    }

    public void Cancel(int askerId)
    {
        if (!invites.TryCancel(askerId, out var targetId))
            return;

        if (zones.TryGetPlayer(targetId, out var target))
            target.Session.Send(new GuildInviteCancelResponse());
    }

    /// <summary>
    ///     <c>CheckCommunityWork()</c>'s six OTHER exclusivity flags (personal shop, duel, trade, friend, party,
    ///     mentor/teacher negotiation -- this family's own guild-negotiation flag is checked separately, and
    ///     atomically, by <see cref="GuildInviteRegistry.TryAsk" />), plus the independent stun/post-death
    ///     action-state gate. Any one of these being true excludes <paramref name="player" /> from starting or
    ///     receiving a guild ask, exactly as it does for every other ASK family in this document.
    /// </summary>
    private bool IsExcludedByCommunityWorkOrStunDeath(PlayerRuntimeState player)
    {
        return player.PshopOpen
               || duels.IsNegotiating(player.CharacterId)
               || trades.IsBusy(player.CharacterId)
               || friends.IsNegotiating(player.CharacterId)
               || parties.IsNegotiating(player.CharacterId)
               || mentors.IsNegotiating(player.CharacterId)
               || player.IsStunned
               || player.IsDead;
    }

    private static PlayerRuntimeState? FindPlayerByName(Zone zone, string avatarName)
    {
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, avatarName, StringComparison.OrdinalIgnoreCase))
                return candidate;

        return null;
    }
}

using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

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
    MentorRegistry mentors,
    ILogger<GuildInviteService> logger) : IGuildInviteService
{
    public GuildInviteAskResultKind Ask(Zone zone, PlayerRuntimeState asker, string targetAvatarName)
    {
        if (asker.GuildId is null || !GuildRoleCodec.IsMasterOrSubMaster(asker.GuildRoleDb))
        {
            logger.LogDebug(
                "Character {CharacterId} guild invite-ask rejected: caller is not guild master/sub-master",
                asker.CharacterId);
            return GuildInviteAskResultKind.NotAuthorized;
        }

        if (IsExcludedByCommunityWorkOrStunDeath(asker))
        {
            logger.LogDebug(
                "Character {CharacterId} guild invite-ask rejected: caller busy (community-work/stun/death gate)",
                asker.CharacterId);
            return GuildInviteAskResultKind.AskerBusy;
        }

        var target = FindPlayerByName(zone, targetAvatarName);
        if (target is null)
        {
            logger.LogDebug("Character {CharacterId} guild invite-ask target {TargetName} not found in zone",
                asker.CharacterId, targetAvatarName);
            return GuildInviteAskResultKind.TargetNotFound;
        }

        if (target.GuildId is not null)
        {
            logger.LogDebug(
                "Character {CharacterId} guild invite-ask rejected: target {TargetCharacterId} already guilded",
                asker.CharacterId, target.CharacterId);
            return GuildInviteAskResultKind.TargetAlreadyGuilded;
        }

        if (asker.Tribe != target.Tribe)
        {
            logger.LogDebug(
                "Character {CharacterId} guild invite-ask rejected: tribe mismatch with target {TargetCharacterId}",
                asker.CharacterId, target.CharacterId);
            return GuildInviteAskResultKind.TribeMismatch;
        }

        if (IsExcludedByCommunityWorkOrStunDeath(target))
        {
            logger.LogDebug(
                "Character {CharacterId} guild invite-ask rejected: target {TargetCharacterId} busy",
                asker.CharacterId, target.CharacterId);
            return GuildInviteAskResultKind.TargetBusy;
        }

        switch (invites.TryAsk(asker.CharacterId, target.CharacterId))
        {
            case GuildInviteAskOutcome.AskerBusy:
                logger.LogDebug(
                    "Character {CharacterId} guild invite-ask rejected: caller already has a pending negotiation",
                    asker.CharacterId);
                return GuildInviteAskResultKind.AskerBusy;
            case GuildInviteAskOutcome.TargetBusy:
                logger.LogDebug(
                    "Character {CharacterId} guild invite-ask rejected: target {TargetCharacterId} already has a pending negotiation",
                    asker.CharacterId, target.CharacterId);
                return GuildInviteAskResultKind.TargetBusy;
            case GuildInviteAskOutcome.Sent:
                target.Session.Send(new GuildInviteResponse { AvatarName = asker.Name });
                logger.LogInformation("Character {CharacterId} sent a guild invite to character {TargetCharacterId}",
                    asker.CharacterId, target.CharacterId);
                return GuildInviteAskResultKind.Sent;
            default:
                return GuildInviteAskResultKind.AskerBusy;
        }
    }

    public void Answer(int targetId, int answerCode)
    {
        if (!invites.TryAnswer(targetId, answerCode == 0, out var askerId))
        {
            logger.LogDebug("Character {TargetId} guild invite answer ignored: no pending invite found", targetId);
            return;
        }

        if (zones.TryGetPlayer(askerId, out var asker))
            asker.Session.Send(new GuildInviteAnswerResponse { Answer = answerCode });

        logger.LogInformation(
            "Character {TargetId} answered guild invite from character {AskerId}: accepted={Accepted}", targetId,
            askerId, answerCode == 0);
    }

    public void Cancel(int askerId)
    {
        if (!invites.TryCancel(askerId, out var targetId))
        {
            logger.LogDebug("Character {AskerId} guild invite cancel ignored: no pending invite found", askerId);
            return;
        }

        if (zones.TryGetPlayer(targetId, out var target))
            target.Session.Send(new GuildInviteCancelResponse());

        logger.LogInformation("Character {AskerId} cancelled guild invite to character {TargetId}", askerId,
            targetId);
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

using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.Social.Duel;
using Fenrir.Application.Game.Domain.Social.Friends;
using Fenrir.Application.Game.Domain.Social.Mentor;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Social;

public sealed class GuildInviteService(
    ZoneRegistry zones,
    GuildInviteRegistry invites,
    DuelRegistry duels,
    TradeRegistry trades,
    FriendRegistry friends,
    PartyRegistry parties,
    MentorRegistry mentors,
    ICharacterShardLocationRepository characterShardLocations,
    ISocialCrossShardRelayQueue crossShardRelay,
    IOptions<GameServerOptions> options,
    ILogger<GuildInviteService> logger) : IGuildInviteService
{
    public async ValueTask<GuildInviteAskResultKind> AskAsync(PlayerRuntimeState asker,
        string targetAvatarName, CancellationToken cancellationToken)
    {
        if (asker.GuildId is null || !GuildRoleCodec.IsMasterOrSubMaster(asker.GuildRoleDb))
        {
            logger.LogDebug(
                "Character {CharacterId} guild invite-ask rejected: caller is not guild master/sub-master",
                asker.CharacterId);
            return GuildInviteAskResultKind.NotAuthorized;
        }

        if (CommunityWorkGate.IsBusy(asker, duels, trades, friends, parties, mentors, invites) ||
            asker.IsStunned || asker.IsDead)
        {
            logger.LogDebug(
                "Character {CharacterId} guild invite-ask rejected: caller busy (community-work/stun/death gate)",
                asker.CharacterId);
            return GuildInviteAskResultKind.AskerBusy;
        }

        if (!zones.TryGetPlayerByName(targetAvatarName, out var target))
            return await AskCrossShardAsync(asker, targetAvatarName, cancellationToken).ConfigureAwait(false);

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

        if (CommunityWorkGate.IsBusy(target, duels, trades, friends, parties, mentors, invites) ||
            target.IsStunned || target.IsDead || target.IsMovingZone)
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
        if (invites.TryConsumeCrossShardInbound(targetId, out var inbound))
        {
            var crossShardAccepted = answerCode == 0;

            var targetName = zones.TryGetPlayer(targetId, out var targetState) ? targetState.Name : "";

            crossShardRelay.Enqueue(new SocialCrossShardRelayEntry(
                SocialCrossShardRelayKind.GuildInvite,
                SocialCrossShardRelayMessageType.Answer,
                crossShardAccepted,
                null,
                options.Value.ShardId,
                targetId,
                targetName,
                inbound.SourceShardId,
                inbound.SourceCharacterId,
                inbound.RelayId));

            logger.LogInformation(
                "Character {TargetId} answered cross-shard guild invite from asker {AskerId} on shard {AskerShardId}: accepted={Accepted}",
                targetId, inbound.SourceCharacterId, inbound.SourceShardId, crossShardAccepted);
            return;
        }

        if (!invites.TryPeekPending(targetId, out var pendingAskerId, out var isAsker) || isAsker)
        {
            logger.LogDebug("Character {TargetId} guild invite answer ignored: no pending invite found", targetId);
            return;
        }

        var askerBusyByZoneTransfer = !zones.TryGetPlayer(pendingAskerId, out var asker) || asker.IsMovingZone;

        if (!invites.TryAnswer(targetId, answerCode == 0, askerBusyByZoneTransfer, out var askerId,
                out var guardBlocked))
        {
            if (guardBlocked)
                logger.LogDebug(
                    "Character {TargetId} guild invite answer: committed answer {AnswerCode} locally, but asker {AskerId} is unreachable or mid zone-transfer -- no notice sent, asker's own record left as-is",
                    targetId, answerCode, askerId);
            else
                logger.LogDebug("Character {TargetId} guild invite answer ignored: no pending invite found",
                    targetId);

            return;
        }

        if (asker is null)
            return;

        asker.Session.Send(new GuildInviteAnswerResponse { Answer = answerCode });

        logger.LogInformation(
            "Character {TargetId} answered guild invite from character {AskerId}: accepted={Accepted}", targetId,
            askerId, answerCode == 0);
    }

    public void Cancel(int askerId)
    {
        if (!invites.TryWithdrawAsk(askerId, out var targetId))
        {
            logger.LogDebug("Character {AskerId} guild invite cancel ignored: no pending invite found", askerId);
            return;
        }

        if (!zones.TryGetPlayer(targetId, out var target) || target.IsMovingZone ||
            !invites.TryAcknowledgeWithdrawal(targetId, askerId))
        {
            logger.LogDebug(
                "Character {AskerId} guild invite cancel: withdrew locally, but target {TargetId} is unreachable, mid zone-transfer, or no longer reciprocally linked -- left un-notified",
                askerId, targetId);
            return;
        }

        target.Session.Send(new GuildInviteCancelResponse());

        logger.LogInformation("Character {AskerId} cancelled guild invite to character {TargetId}", askerId,
            targetId);
    }

    private async ValueTask<GuildInviteAskResultKind> AskCrossShardAsync(PlayerRuntimeState asker,
        string targetAvatarName, CancellationToken cancellationToken)
    {
        var remote = await characterShardLocations.FindByNameAsync(targetAvatarName, cancellationToken)
            .ConfigureAwait(false);

        if (remote is null)
        {
            logger.LogDebug(
                "Character {CharacterId} guild invite-ask target {TargetName} not found on any shard",
                asker.CharacterId, targetAvatarName);
            return GuildInviteAskResultKind.TargetNotFound;
        }

        if (remote.ShardId == options.Value.ShardId)
        {
            logger.LogWarning(
                "Character {CharacterId} guild invite-ask target {TargetName}: directory reports shard {ShardId} (same as asker's own shard) but the live shard-wide registry has no such player -- treating as a stale directory row, not publishing cross-shard",
                asker.CharacterId, targetAvatarName, remote.ShardId);
            return GuildInviteAskResultKind.TargetNotFound;
        }

        if (asker.Tribe != remote.Tribe)
        {
            logger.LogDebug(
                "Character {CharacterId} guild invite-ask rejected: tribe mismatch with cross-shard target {TargetCharacterId}",
                asker.CharacterId, remote.CharacterId);
            return GuildInviteAskResultKind.TribeMismatch;
        }

        var outcome = invites.TryAskCrossShard(asker.CharacterId,
            new CrossShardOutboundAsk(remote.ShardId, remote.CharacterId, remote.AvatarName));

        if (outcome != GuildInviteAskOutcome.Sent)
        {
            logger.LogDebug(
                "Character {CharacterId} guild invite-ask rejected: caller already has a pending negotiation (cross-shard registration)",
                asker.CharacterId);
            return GuildInviteAskResultKind.AskerBusy;
        }

        crossShardRelay.Enqueue(new SocialCrossShardRelayEntry(
            SocialCrossShardRelayKind.GuildInvite,
            SocialCrossShardRelayMessageType.Ask,
            null,
            null,
            options.Value.ShardId,
            asker.CharacterId,
            asker.Name,
            remote.ShardId,
            remote.CharacterId,
            null));

        logger.LogInformation(
            "Character {CharacterId} published a guild invite cross-shard to character {TargetCharacterId} on shard {TargetShardId}",
            asker.CharacterId, remote.CharacterId, remote.ShardId);
        return GuildInviteAskResultKind.SentCrossShard;
    }
}

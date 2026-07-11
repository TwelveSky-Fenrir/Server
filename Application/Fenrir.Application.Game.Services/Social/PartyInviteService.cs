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
using Fenrir.Application.Game.Domain.World.WorldState;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Social;

public sealed class PartyInviteService(
    ZoneRegistry zones,
    PartyRegistry parties,
    DuelRegistry duels,
    TradeRegistry trades,
    FriendRegistry friends,
    MentorRegistry mentors,
    GuildInviteRegistry guildInvites,
    WorldStateService worldState,
    ICharacterShardLocationRepository characterShardLocations,
    ISocialCrossShardRelayQueue crossShardRelay,
    IOptions<GameServerOptions> options,
    ILogger<PartyInviteService> logger)
    : IPartyInviteService
{
    public async ValueTask<PartyInviteResult> InviteAsync(Zone zone, PlayerRuntimeState inviter,
        string targetAvatarName, CancellationToken cancellationToken)
    {
        if (parties.IsInParty(inviter.CharacterId) && !parties.IsLeader(inviter.CharacterId))
        {
            logger.LogWarning(
                "Party invite rejected: character {InviterCharacterId} is already partied and not the leader -- session will be disconnected",
                inviter.CharacterId);
            return new PartyInviteResult(PartyInviteResultKind.InviterMustDisconnect);
        }

        if (CommunityWorkGate.IsBusy(inviter, duels, trades, friends, parties, mentors, guildInvites) ||
            inviter.IsStunned || inviter.IsDead)
        {
            logger.LogDebug("Party invite rejected: character {InviterCharacterId} already has a pending invite",
                inviter.CharacterId);
            return new PartyInviteResult(PartyInviteResultKind.InviterBusy);
        }

        if (!zones.TryGetPlayerByName(targetAvatarName, out var target))
            return await InviteCrossShardAsync(inviter, targetAvatarName, cancellationToken).ConfigureAwait(false);

        var targetBusyExternally =
            CommunityWorkGate.IsBusy(target, duels, trades, friends, parties, mentors, guildInvites) ||
            target.IsStunned || target.IsDead || target.IsMovingZone;

        var allyOfInviterTribe = worldState.GetAllyOf(inviter.Tribe);

        var outcome = parties.TryInvite(inviter.CharacterId, inviter.CombinedLevel, inviter.Tribe,
            target.CharacterId, target.CombinedLevel, target.Tribe, allyOfInviterTribe, targetBusyExternally);

        switch (outcome)
        {
            case PartyInviteOutcome.InviterMustDisconnect:
                logger.LogWarning(
                    "Party invite rejected: character {InviterCharacterId} must disconnect (registry check against target {TargetCharacterId})",
                    inviter.CharacterId, target.CharacterId);
                return new PartyInviteResult(PartyInviteResultKind.InviterMustDisconnect);
            case PartyInviteOutcome.InviterBusy:
                logger.LogDebug("Party invite rejected: character {InviterCharacterId} is busy",
                    inviter.CharacterId);
                return new PartyInviteResult(PartyInviteResultKind.InviterBusy);
            case PartyInviteOutcome.TargetBusy:
                logger.LogDebug("Party invite rejected: target character {TargetCharacterId} is busy",
                    target.CharacterId);
                return new PartyInviteResult(PartyInviteResultKind.TargetBusy);
            case PartyInviteOutcome.TargetAlreadyPartied:
                logger.LogDebug("Party invite rejected: target character {TargetCharacterId} is already partied",
                    target.CharacterId);
                return new PartyInviteResult(PartyInviteResultKind.TargetAlreadyPartied);
            default:
                logger.LogDebug(
                    "Party invite sent: character {InviterCharacterId} ({InviterName}) -> character {TargetCharacterId} ({TargetName})",
                    inviter.CharacterId, inviter.Name, target.CharacterId, target.Name);
                return new PartyInviteResult(PartyInviteResultKind.Sent, target.CharacterId, target.Name,
                    inviter.Name);
        }
    }

    private async ValueTask<PartyInviteResult> InviteCrossShardAsync(PlayerRuntimeState inviter,
        string targetAvatarName, CancellationToken cancellationToken)
    {
        var remote = await characterShardLocations.FindByNameAsync(targetAvatarName, cancellationToken)
            .ConfigureAwait(false);

        if (remote is null)
        {
            logger.LogDebug(
                "Party invite rejected: character {InviterCharacterId} target {TargetAvatarName} not found on any shard",
                inviter.CharacterId, targetAvatarName);
            return new PartyInviteResult(PartyInviteResultKind.TargetNotFound);
        }

        if (inviter.Tribe != remote.Tribe)
        {
            logger.LogWarning(
                "Party invite rejected: character {InviterCharacterId} (tribe {InviterTribe}) targeted cross-shard character {TargetCharacterId} (tribe {TargetTribe}) -- session will be disconnected",
                inviter.CharacterId, inviter.Tribe, remote.CharacterId, remote.Tribe);
            return new PartyInviteResult(PartyInviteResultKind.InviterMustDisconnect);
        }

        var outcome = parties.TryInviteCrossShard(inviter.CharacterId,
            new CrossShardOutboundAsk(remote.ShardId, remote.CharacterId, remote.AvatarName));

        switch (outcome)
        {
            case PartyInviteOutcome.InviterMustDisconnect:
                logger.LogWarning(
                    "Party invite rejected: character {InviterCharacterId} must disconnect (registry check, cross-shard)",
                    inviter.CharacterId);
                return new PartyInviteResult(PartyInviteResultKind.InviterMustDisconnect);
            case PartyInviteOutcome.Sent:
                crossShardRelay.Enqueue(new SocialCrossShardRelayEntry(
                    SocialCrossShardRelayKind.Party,
                    SocialCrossShardRelayMessageType.Ask,
                    null,
                    null,
                    options.Value.ShardId,
                    inviter.CharacterId,
                    inviter.Name,
                    remote.ShardId,
                    remote.CharacterId,
                    null));

                logger.LogDebug(
                    "Party invite published cross-shard: character {InviterCharacterId} ({InviterName}) -> character {TargetCharacterId} on shard {TargetShardId}",
                    inviter.CharacterId, inviter.Name, remote.CharacterId, remote.ShardId);
                return new PartyInviteResult(PartyInviteResultKind.SentCrossShard);
            default:
                logger.LogDebug(
                    "Party invite rejected: character {InviterCharacterId} is busy (cross-shard registration)",
                    inviter.CharacterId);
                return new PartyInviteResult(PartyInviteResultKind.InviterBusy);
        }
    }
}

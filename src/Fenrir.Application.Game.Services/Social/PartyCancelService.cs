using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Social;

public sealed class PartyCancelService(
    PartyRegistry parties,
    ZoneRegistry zones,
    ISocialCrossShardRelayQueue crossShardRelay,
    IOptions<GameServerOptions> options,
    ILogger<PartyCancelService> logger) : IPartyCancelService
{
    public PartyCancelResult Cancel(int inviterId)
    {
        if (!parties.TryCancel(inviterId, out var inviteeId, out var crossShardAsk))
        {
            logger.LogDebug("Party cancel ignored: character {InviterId} has no pending invite to cancel",
                inviterId);
            return new PartyCancelResult(false, 0);
        }

        if (crossShardAsk is { } remote)
        {
            var inviterName = zones.TryGetPlayer(inviterId, out var inviter) ? inviter.Name : "";

            crossShardRelay.Enqueue(new SocialCrossShardRelayEntry(
                SocialCrossShardRelayKind.Party,
                SocialCrossShardRelayMessageType.Cancel,
                null,
                null,
                options.Value.ShardId,
                inviterId,
                inviterName,
                remote.TargetShardId,
                remote.TargetCharacterId,
                null));

            logger.LogDebug(
                "Cross-shard party invite cancelled: character {InviterId} withdrew invite to character {InviteeId} on shard {InviteeShardId}",
                inviterId, inviteeId, remote.TargetShardId);
            return new PartyCancelResult(true, inviteeId, true);
        }

        if (!zones.TryGetPlayer(inviteeId, out var invitee) || invitee.IsMovingZone)
        {
            logger.LogDebug(
                "Party cancel: character {InviterId} withdrew its own pending invite, but counterpart {InviteeId} is unreachable or mid zone-transfer -- no notice sent, counterpart's record left as-is",
                inviterId, inviteeId);
            return new PartyCancelResult(false, 0);
        }

        parties.ClearInviteeAfterCancel(inviteeId, inviterId);

        logger.LogDebug("Party invite cancelled: character {InviterId} withdrew invite to character {InviteeId}",
            inviterId, inviteeId);
        return new PartyCancelResult(true, inviteeId);
    }
}

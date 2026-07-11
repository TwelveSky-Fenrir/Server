using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

public sealed class PartyCancelService(PartyRegistry parties, ZoneRegistry zones, ILogger<PartyCancelService> logger)
    : IPartyCancelService
{
    public PartyCancelResult Cancel(int inviterId)
    {
        if (!parties.TryCancel(inviterId, out var inviteeId))
        {
            logger.LogDebug("Party cancel ignored: character {InviterId} has no pending invite to cancel",
                inviterId);
            return new PartyCancelResult(false, 0);
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

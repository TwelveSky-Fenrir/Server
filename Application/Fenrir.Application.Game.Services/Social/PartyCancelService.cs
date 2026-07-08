using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Party;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Social;

public sealed class PartyCancelService(PartyRegistry parties, ILogger<PartyCancelService> logger)
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

        logger.LogDebug("Party invite cancelled: character {InviterId} withdrew invite to character {InviteeId}",
            inviterId, inviteeId);
        return new PartyCancelResult(true, inviteeId);
    }
}

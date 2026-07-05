using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Party;

namespace Fenrir.Application.Game.Services.Social;

public sealed class PartyCancelService(PartyRegistry parties) : IPartyCancelService
{
    public PartyCancelResult Cancel(int inviterId)
    {
        return parties.TryCancel(inviterId, out var inviteeId)
            ? new PartyCancelResult(true, inviteeId)
            : new PartyCancelResult(false, 0);
    }
}

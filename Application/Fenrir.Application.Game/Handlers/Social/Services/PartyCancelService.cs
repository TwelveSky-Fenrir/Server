using Fenrir.Application.Game.Social.Party;

namespace Fenrir.Application.Game.Handlers.Social.Services;

/// <summary>Outcome of a CZ_PARTY_CANCEL_SEND attempt.</summary>
public readonly record struct PartyCancelResult(bool Handled, int InviteeId);

public interface IPartyCancelService
{
    PartyCancelResult Cancel(int inviterId);
}

public sealed class PartyCancelService(PartyRegistry parties) : IPartyCancelService
{
    public PartyCancelResult Cancel(int inviterId)
    {
        return parties.TryCancel(inviterId, out var inviteeId)
            ? new PartyCancelResult(true, inviteeId)
            : new PartyCancelResult(false, 0);
    }
}

namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>Outcome of a CZ_PARTY_CANCEL_SEND attempt.</summary>
public readonly record struct PartyCancelResult(bool Handled, int InviteeId);

public interface IPartyCancelService
{
    public PartyCancelResult Cancel(int inviterId);
}

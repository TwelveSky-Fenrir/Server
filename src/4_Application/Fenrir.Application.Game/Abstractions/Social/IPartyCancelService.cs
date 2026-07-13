namespace Fenrir.Application.Game.Abstractions.Social;

public readonly record struct PartyCancelResult(bool Handled, int InviteeId);

public interface IPartyCancelService
{
    public PartyCancelResult Cancel(int inviterId);
}

namespace Fenrir.Application.Game.Abstractions.Social;

public readonly record struct PartyCancelResult(bool Handled, int InviteeId, bool IsCrossShard = false);

public interface IPartyCancelService
{
    public PartyCancelResult Cancel(int inviterId);
}

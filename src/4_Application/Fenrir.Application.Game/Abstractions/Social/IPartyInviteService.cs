using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

public enum PartyInviteResultKind
{
    InviterMustDisconnect,
    TargetNotFound,
    InviterBusy,
    TargetBusy,
    TargetAlreadyPartied,
    Sent,
    SentCrossShard
}

public readonly record struct PartyInviteResult(
    PartyInviteResultKind Kind,
    int TargetCharacterId = 0,
    string? TargetName = null,
    string? InviterName = null);

public interface IPartyInviteService
{
    public ValueTask<PartyInviteResult> InviteAsync(Zone zone, PlayerRuntimeState inviter, string targetAvatarName,
        CancellationToken cancellationToken);
}

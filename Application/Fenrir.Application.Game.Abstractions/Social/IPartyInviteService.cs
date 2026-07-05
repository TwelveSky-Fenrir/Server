using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Social;

/// <summary>Discriminator for how a CZ_PARTY_ASK_SEND attempt resolved.</summary>
public enum PartyInviteResultKind
{
    InviterMustDisconnect,
    TargetNotFound,
    InviterBusy,
    TargetBusy,
    TargetAlreadyPartied,
    Sent
}

public readonly record struct PartyInviteResult(
    PartyInviteResultKind Kind,
    int TargetCharacterId = 0,
    string? TargetName = null,
    string? InviterName = null);

public interface IPartyInviteService
{
    public PartyInviteResult Invite(Zone zone, PlayerRuntimeState inviter, string targetAvatarName);
}

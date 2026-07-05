using Fenrir.Application.Game.Social.Party;
using Fenrir.Application.Game.World;

namespace Fenrir.Application.Game.Handlers.Social.Services;

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
    PartyInviteResult Invite(Zone zone, PlayerRuntimeState inviter, string targetAvatarName);
}

/// <summary>
///     Level check uses <see cref="PlayerRuntimeState.Level" /> alone; aLevel2 (legacy's rebirth sub-level)
///     isn't modeled.
/// </summary>
public sealed class PartyInviteService(PartyRegistry parties) : IPartyInviteService
{
    public PartyInviteResult Invite(Zone zone, PlayerRuntimeState inviter, string targetAvatarName)
    {
        PlayerRuntimeState? target = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, targetAvatarName, StringComparison.OrdinalIgnoreCase))
            {
                target = candidate;
                break;
            }

        if (target is null)
            return new PartyInviteResult(PartyInviteResultKind.TargetNotFound);

        var outcome = parties.TryInvite(inviter.CharacterId, inviter.Level, inviter.Tribe, target.CharacterId,
            target.Level, target.Tribe);

        return outcome switch
        {
            PartyInviteOutcome.InviterMustDisconnect => new PartyInviteResult(PartyInviteResultKind.InviterMustDisconnect),
            PartyInviteOutcome.InviterBusy => new PartyInviteResult(PartyInviteResultKind.InviterBusy),
            PartyInviteOutcome.TargetBusy => new PartyInviteResult(PartyInviteResultKind.TargetBusy),
            PartyInviteOutcome.TargetAlreadyPartied => new PartyInviteResult(PartyInviteResultKind.TargetAlreadyPartied),
            _ => new PartyInviteResult(PartyInviteResultKind.Sent, target.CharacterId, target.Name, inviter.Name)
        };
    }
}

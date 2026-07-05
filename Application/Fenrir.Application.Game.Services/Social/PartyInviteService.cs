using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.Social;

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
            PartyInviteOutcome.InviterMustDisconnect => new PartyInviteResult(PartyInviteResultKind
                .InviterMustDisconnect),
            PartyInviteOutcome.InviterBusy => new PartyInviteResult(PartyInviteResultKind.InviterBusy),
            PartyInviteOutcome.TargetBusy => new PartyInviteResult(PartyInviteResultKind.TargetBusy),
            PartyInviteOutcome.TargetAlreadyPartied =>
                new PartyInviteResult(PartyInviteResultKind.TargetAlreadyPartied),
            _ => new PartyInviteResult(PartyInviteResultKind.Sent, target.CharacterId, target.Name, inviter.Name)
        };
    }
}

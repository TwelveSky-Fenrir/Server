using Fenrir.Application.Game.Social.Party;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_PARTY_ASK_SEND (opcode 65) -- level check uses <see cref="PlayerRuntimeState.Level" /> alone;
///     aLevel2 (legacy's rebirth sub-level) isn't modeled.
/// </summary>
public sealed class PartyInviteHandler(PartyRegistry parties) : IInlinePacketHandler<PartyInviteRequest>
{
    public void Handle(in PartyInviteRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var inviterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(inviterId, out var inviter) || inviter is null)
            return;

        PlayerRuntimeState? target = null;
        foreach (var candidate in zone.Players)
            if (string.Equals(candidate.Name, packet.AvatarName, StringComparison.OrdinalIgnoreCase))
            {
                target = candidate;
                break;
            }

        if (target is null)
        {
            session.Send(new PartyAnswerResponse { Answer = 4 });
            return;
        }

        var outcome = parties.TryInvite(inviterId, inviter.Level, inviter.Tribe, target.CharacterId, target.Level,
            target.Tribe);

        switch (outcome)
        {
            case PartyInviteOutcome.InviterMustDisconnect:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case PartyInviteOutcome.InviterBusy:
                session.Send(new PartyAnswerResponse { Answer = 3 });
                return;
            case PartyInviteOutcome.TargetBusy:
                session.Send(new PartyAnswerResponse { Answer = 5 });
                return;
            case PartyInviteOutcome.TargetAlreadyPartied:
                session.Send(new PartyAnswerResponse { Answer = 6 });
                return;
            case PartyInviteOutcome.Sent:
                target.Session.Send(new PartyInviteResponse { AvatarName = inviter.Name });
                return;
        }
    }
}

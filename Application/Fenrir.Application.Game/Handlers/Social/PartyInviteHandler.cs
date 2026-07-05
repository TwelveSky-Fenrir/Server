using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_PARTY_ASK_SEND (opcode 65) -- level check uses <see cref="PlayerRuntimeState.Level" /> alone;
///     aLevel2 (legacy's rebirth sub-level) isn't modeled.
/// </summary>
public sealed class PartyInviteHandler(IPartyInviteService partyInviteService) : IInlinePacketHandler<PartyInviteRequest>
{
    public void Handle(in PartyInviteRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var inviterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(inviterId, out var inviter) || inviter is null)
            return;

        var result = partyInviteService.Invite(zone, inviter, packet.AvatarName);

        switch (result.Kind)
        {
            case PartyInviteResultKind.InviterMustDisconnect:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case PartyInviteResultKind.TargetNotFound:
                session.Send(new PartyAnswerResponse { Answer = 4 });
                return;
            case PartyInviteResultKind.InviterBusy:
                session.Send(new PartyAnswerResponse { Answer = 3 });
                return;
            case PartyInviteResultKind.TargetBusy:
                session.Send(new PartyAnswerResponse { Answer = 5 });
                return;
            case PartyInviteResultKind.TargetAlreadyPartied:
                session.Send(new PartyAnswerResponse { Answer = 6 });
                return;
            case PartyInviteResultKind.Sent:
                zone.TryGetPlayer(result.TargetCharacterId, out var target);
                target!.Session.Send(new PartyInviteResponse { AvatarName = result.InviterName! });
                return;
        }
    }
}

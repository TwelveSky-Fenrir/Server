using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>CZ_PARTY_CANCEL_SEND (opcode 66) -- withdraws the caller's own still-pending ask.</summary>
public sealed class PartyCancelHandler(ZoneRegistry zones, IPartyCancelService partyCancelService)
    : IInlinePacketHandler<PartyCancelRequest>
{
    public void Handle(in PartyCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var inviterId = zoneSession.CharacterId!.Value;

        var result = partyCancelService.Cancel(inviterId);
        if (!result.Handled)
            return;

        if (zones.TryGetPlayer(result.InviteeId, out var invitee))
            invitee.Session.Send(new PartyCancelResponse());
    }
}

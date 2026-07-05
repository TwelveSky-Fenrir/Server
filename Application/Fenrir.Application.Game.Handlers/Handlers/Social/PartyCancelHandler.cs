using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

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

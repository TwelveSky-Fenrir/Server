using Fenrir.Application.Game.Social.Party;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>CZ_PARTY_CANCEL_SEND (opcode 66) -- withdraws the caller's own still-pending ask.</summary>
public sealed class PartyCancelHandler(ZoneRegistry zones, PartyRegistry parties)
    : IInlinePacketHandler<PartyCancelRequest>
{
    public void Handle(in PartyCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var inviterId = zoneSession.CharacterId!.Value;

        if (!parties.TryCancel(inviterId, out var inviteeId))
            return;

        if (zones.TryGetPlayer(inviteeId, out var invitee))
            invitee.Session.Send(new PartyCancelResponse());
    }
}

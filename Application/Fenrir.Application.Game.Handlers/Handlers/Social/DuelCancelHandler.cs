using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>CZ_DUEL_CANCEL_SEND (opcode 44) -- the challenger withdraws their own still-pending ask.</summary>
public sealed class DuelCancelHandler(IDuelService duelService) : IInlinePacketHandler<DuelCancelRequest>
{
    public void Handle(in DuelCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var challengerId = zoneSession.CharacterId!.Value;

        duelService.Cancel(challengerId);
    }
}

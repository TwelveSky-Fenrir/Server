using Fenrir.Application.Game.Social.Duel;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

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

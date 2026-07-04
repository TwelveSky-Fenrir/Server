using Fenrir.Application.Game.Social.Duel;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>CZ_DUEL_CANCEL_SEND (opcode 44) -- the challenger withdraws their own still-pending ask.</summary>
public sealed class DuelCancelHandler(ZoneRegistry zones, DuelRegistry duels) : IInlinePacketHandler<DuelCancelRequest>
{
    public void Handle(in DuelCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var challengerId = zoneSession.CharacterId!.Value;

        if (!duels.TryCancel(challengerId, out var targetId))
            return;

        if (zones.TryGetPlayer(targetId, out var target))
            target.Session.Send(new DuelCancelResponse());
    }
}

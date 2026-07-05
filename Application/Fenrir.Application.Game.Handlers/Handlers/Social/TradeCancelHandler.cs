using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>CZ_TRADE_CANCEL_SEND (opcode 48) -- the asker withdraws their own still-pending ask.</summary>
public sealed class TradeCancelHandler(ZoneRegistry zones, ITradeCancelService tradeCancelService)
    : IInlinePacketHandler<TradeCancelRequest>
{
    public void Handle(in TradeCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var askerId = zoneSession.CharacterId!.Value;

        var result = tradeCancelService.Cancel(askerId);
        if (!result.Handled)
            return;

        if (zones.TryGetPlayer(result.TargetId, out var target))
            target.Session.Send(new TradeCancelResponse());
    }
}

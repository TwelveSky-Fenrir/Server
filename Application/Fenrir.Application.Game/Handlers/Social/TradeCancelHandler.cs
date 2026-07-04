using Fenrir.Application.Game.Social.Trade;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>CZ_TRADE_CANCEL_SEND (opcode 48) -- the asker withdraws their own still-pending ask.</summary>
public sealed class TradeCancelHandler(ZoneRegistry zones, TradeRegistry trades) : IInlinePacketHandler<TradeCancelRequest>
{
    public void Handle(in TradeCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var askerId = zoneSession.CharacterId!.Value;

        if (!trades.TryCancel(askerId, out var targetId))
            return;

        if (zones.TryGetPlayer(targetId, out var target))
            target.Session.Send(new TradeCancelResponse());
    }
}

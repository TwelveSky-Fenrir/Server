using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>
///     CZ_TRADE_START_SEND (opcode 50) -- callable by either accepted side; ZC_TRADE_START_RECV is crossed
///     (each player receives the OTHER's offer).
/// </summary>
public sealed class TradeStartHandler(ZoneRegistry zones, ITradeStartService tradeStartService)
    : IInlinePacketHandler<TradeStartRequest>
{
    public void Handle(in TradeStartRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var callerId = zoneSession.CharacterId!.Value;

        var result = tradeStartService.Start(callerId);
        if (!result.Handled)
            return;

        var trade = result.Trade!;

        if (!zones.TryGetPlayer(trade.PlayerAId, out var playerA) ||
            !zones.TryGetPlayer(trade.PlayerBId, out var playerB))
            return;

        playerA.Session.Send(TradeOfferCodec.BuildStart(trade.SideB));
        playerB.Session.Send(TradeOfferCodec.BuildStart(trade.SideA));
    }
}

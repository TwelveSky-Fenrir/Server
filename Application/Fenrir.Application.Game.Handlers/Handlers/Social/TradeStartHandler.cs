using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>
///     CZ_TRADE_START_SEND (opcode 50) -- callable by either accepted side; ZC_TRADE_START_RECV is crossed
///     (each player receives the OTHER's offer).
/// </summary>
public sealed class TradeStartHandler(
    ZoneRegistry zones,
    ITradeStartService tradeStartService,
    ILogger<TradeStartHandler> logger) : IInlinePacketHandler<TradeStartRequest>
{
    public void Handle(in TradeStartRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var callerId = zoneSession.CharacterId!.Value;

        logger.LogDebug("TradeStart: session {SessionId} character {CharacterId}", session.SessionId, callerId);

        var result = tradeStartService.Start(callerId);
        if (!result.Handled)
            return;

        var trade = result.Trade!;

        if (!zones.TryGetPlayer(trade.PlayerAId, out var playerA) ||
            !zones.TryGetPlayer(trade.PlayerBId, out var playerB))
        {
            // Partner re-validation failed after TradeRegistry.TryStart already committed both sides to
            // trade-process-state 4 -- roll the CALLER's own state back to idle only; the partner's side
            // (if any) is deliberately left untouched, matching legacy's asymmetric partial-effect behavior.
            tradeStartService.AbortStart(callerId);
            return;
        }

        playerA.Session.Send(TradeOfferCodec.BuildStart(trade.SideB));
        playerB.Session.Send(TradeOfferCodec.BuildStart(trade.SideA));
    }
}

using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

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
            tradeStartService.AbortStart(callerId);
            return;
        }

        playerA.Session.Send(TradeOfferCodec.BuildStart(trade.SideB));
        playerB.Session.Send(TradeOfferCodec.BuildStart(trade.SideA));
    }
}

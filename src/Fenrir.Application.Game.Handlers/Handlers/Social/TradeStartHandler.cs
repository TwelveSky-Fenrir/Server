using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Social.Trade;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class TradeStartHandler(
    ITradeStartService tradeStartService,
    ILogger<TradeStartHandler> logger) : IInlinePacketHandler<TradeStartRequest>
{
    public void Handle(in TradeStartRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        var callerId = zoneSession.CharacterId!.Value;

        logger.LogDebug("TradeStart: session {SessionId} character {CharacterId}", session.SessionId, callerId);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var result = tradeStartService.Start(callerId);
        if (!result.Handled)
            return;

        var trade = result.Trade!;

        if (!zone.TryGetPlayer(trade.PlayerAId, out var playerA) || playerA is null || playerA.IsMovingZone ||
            !zone.TryGetPlayer(trade.PlayerBId, out var playerB) || playerB is null || playerB.IsMovingZone)
        {
            tradeStartService.AbortStart(callerId);
            return;
        }

        playerA.Session.Send(TradeOfferCodec.BuildStart(trade.SideB));
        playerB.Session.Send(TradeOfferCodec.BuildStart(trade.SideA));
    }
}

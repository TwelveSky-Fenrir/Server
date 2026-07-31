using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class TradeCancelHandler(
    ZoneRegistry zones,
    ITradeCancelService tradeCancelService,
    ILogger<TradeCancelHandler> logger) : IInlinePacketHandler<TradeCancelRequest>
{
    public void Handle(in TradeCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        var askerId = zoneSession.CharacterId!.Value;

        logger.LogDebug("TradeCancel: session {SessionId} character {CharacterId}", session.SessionId, askerId);

        var result = tradeCancelService.Cancel(askerId);
        if (!result.Handled)
            return;

        if (zones.TryGetPlayer(result.TargetId, out var target))
            target.Session.Send(new TradeCancelResponse());
    }
}

using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class TradeEndHandler(
    ZoneRegistry zones,
    ITradeEndService tradeEndService,
    ILogger<TradeEndHandler> logger) : IInlinePacketHandler<TradeEndRequest>
{
    public void Handle(in TradeEndRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug("TradeEnd: session {SessionId} character {CharacterId}", session.SessionId, characterId);

        var result = tradeEndService.End(characterId);
        if (!result.Handled)
            return;

        var response = new TradeEndResponse { Result = 1 };

        if (zones.TryGetPlayer(result.PlayerAId, out var playerA))
            playerA.Session.Send(response);
        if (zones.TryGetPlayer(result.PlayerBId, out var playerB))
            playerB.Session.Send(response);
    }
}

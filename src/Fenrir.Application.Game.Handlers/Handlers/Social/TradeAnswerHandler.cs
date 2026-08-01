using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class TradeAnswerHandler(
    ZoneRegistry zones,
    ITradeAnswerService tradeAnswerService,
    ILogger<TradeAnswerHandler> logger) : IInlinePacketHandler<TradeAnswerRequest>
{
    public void Handle(in TradeAnswerRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        var targetId = zoneSession.CharacterId!.Value;

        logger.LogDebug("TradeAnswer: session {SessionId} character {CharacterId} answer {Answer}",
            session.SessionId, targetId, packet.Answer);

        var result = tradeAnswerService.Answer(targetId, packet.Answer);
        if (!result.Handled)
            return;

        if (zones.TryGetPlayer(result.AskerId, out var asker))
            asker.Session.Send(new TradeAnswerResponse { Answer = packet.Answer });
    }
}

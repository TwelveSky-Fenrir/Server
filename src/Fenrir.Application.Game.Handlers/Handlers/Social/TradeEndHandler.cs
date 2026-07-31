using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class TradeEndHandler(
    ZoneRegistry zones,
    ITradeEndService tradeEndService,
    ILogger<TradeEndHandler> logger) : IInlinePacketHandler<TradeEndRequest>
{
    public void Handle(in TradeEndRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug("TradeEnd: session {SessionId} character {CharacterId}", session.SessionId, characterId);

        var result = tradeEndService.End(characterId);
        if (!result.Handled)
            return;

        RestoreStagedBigMoney(result.PlayerAId, result.PlayerABigMoneyRestore);
        RestoreStagedBigMoney(result.PlayerBId, result.PlayerBBigMoneyRestore);

        var response = new TradeEndResponse { Result = 1 };

        if (zones.TryGetPlayer(result.PlayerAId, out var playerA))
            playerA.Session.Send(response);
        if (zones.TryGetPlayer(result.PlayerBId, out var playerB))
            playerB.Session.Send(response);
    }

    private void RestoreStagedBigMoney(int characterId, int amount)
    {
        if (amount == 0)
            return;

        if (zones.TryGetPlayerAndZone(characterId, out _, out var zone))
            zone.PostTribeProgressCommand(new TribeProgressZoneCommand(characterId, BigMoneyDelta: amount));
    }
}

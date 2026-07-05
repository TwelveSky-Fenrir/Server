using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_TRADE_END_SEND (opcode 52) -- abandons an in-progress trade; nothing was ever committed, so no rollback is
///     needed.
/// </summary>
public sealed class TradeEndHandler(ZoneRegistry zones, ITradeEndService tradeEndService)
    : IInlinePacketHandler<TradeEndRequest>
{
    public void Handle(in TradeEndRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

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

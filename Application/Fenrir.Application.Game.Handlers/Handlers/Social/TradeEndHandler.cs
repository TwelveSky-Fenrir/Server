using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

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

using Fenrir.Application.Game.Social.Trade;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>CZ_TRADE_END_SEND (opcode 52) -- abandons an in-progress trade; nothing was ever committed, so no rollback is needed.</summary>
public sealed class TradeEndHandler(ZoneRegistry zones, TradeRegistry trades) : IInlinePacketHandler<TradeEndRequest>
{
    public void Handle(in TradeEndRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (!trades.TryEnd(characterId, out var trade) || trade is null)
            return;

        var result = new TradeEndResponse { Result = 1 };

        if (zones.TryGetPlayer(trade.PlayerAId, out var playerA))
            playerA.Session.Send(result);
        if (zones.TryGetPlayer(trade.PlayerBId, out var playerB))
            playerB.Session.Send(result);
    }
}

using Fenrir.Application.Game.Social.Trade;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_TRADE_START_SEND (opcode 50) -- callable by EITHER accepted side (both were already marked
///     accepted at answer time, see <see cref="TradeRegistry" />'s own remarks). Allocates a fresh, EMPTY
///     <see cref="TradeSession" /> and sends ZC_TRADE_START_RECV CROSSED (contract's own "chaque joueur
///     reçoit l'offre de L'AUTRE") -- both offers start empty, so this is a zeroed payload either way.
/// </summary>
public sealed class TradeStartHandler(ZoneRegistry zones, TradeRegistry trades) : IInlinePacketHandler<TradeStartRequest>
{
    public void Handle(in TradeStartRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var callerId = zoneSession.CharacterId!.Value;

        if (!trades.TryStart(callerId, out var trade))
            return;

        if (!zones.TryGetPlayer(trade.PlayerAId, out var playerA) ||
            !zones.TryGetPlayer(trade.PlayerBId, out var playerB))
            return;

        playerA.Session.Send(TradeOfferCodec.BuildStart(trade.SideB));
        playerB.Session.Send(TradeOfferCodec.BuildStart(trade.SideA));
    }
}

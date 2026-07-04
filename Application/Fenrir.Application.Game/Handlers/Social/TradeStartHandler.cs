using Fenrir.Application.Game.Social.Trade;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_TRADE_START_SEND (opcode 50) -- callable by either accepted side (both already marked accepted
///     at answer time, see <see cref="TradeRegistry" />). Allocates a fresh, empty
///     <see cref="TradeSession" /> and sends ZC_TRADE_START_RECV crossed (each player receives the OTHER's
///     offer) -- both start empty, so it's a zeroed payload either way.
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

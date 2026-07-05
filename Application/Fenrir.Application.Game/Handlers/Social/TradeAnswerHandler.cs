using Fenrir.Application.Game.Social.Trade;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>CZ_TRADE_ANSWER_SEND (opcode 49) -- on accept, both sides may send CZ_TRADE_START_SEND (symmetric).</summary>
public sealed class TradeAnswerHandler(ZoneRegistry zones, TradeRegistry trades)
    : IInlinePacketHandler<TradeAnswerRequest>
{
    public void Handle(in TradeAnswerRequest packet, IPacketSession session)
    {
        if (packet.Answer is not (0 or 1 or 2))
            return;

        var zoneSession = (ZoneClientSession)session;
        var targetId = zoneSession.CharacterId!.Value;

        if (!trades.TryAnswer(targetId, packet.Answer == 0, out var askerId))
            return;

        if (zones.TryGetPlayer(askerId, out var asker))
            asker.Session.Send(new TradeAnswerResponse { Answer = packet.Answer });
    }
}

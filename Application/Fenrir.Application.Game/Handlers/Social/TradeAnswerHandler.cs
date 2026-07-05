using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>CZ_TRADE_ANSWER_SEND (opcode 49) -- on accept, both sides may send CZ_TRADE_START_SEND (symmetric).</summary>
public sealed class TradeAnswerHandler(ZoneRegistry zones, ITradeAnswerService tradeAnswerService)
    : IInlinePacketHandler<TradeAnswerRequest>
{
    public void Handle(in TradeAnswerRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var targetId = zoneSession.CharacterId!.Value;

        var result = tradeAnswerService.Answer(targetId, packet.Answer);
        if (!result.Handled)
            return;

        if (zones.TryGetPlayer(result.AskerId, out var asker))
            asker.Session.Send(new TradeAnswerResponse { Answer = packet.Answer });
    }
}

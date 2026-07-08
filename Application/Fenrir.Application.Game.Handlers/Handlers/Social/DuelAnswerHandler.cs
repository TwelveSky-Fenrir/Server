using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>CZ_DUEL_ANSWER_SEND (opcode 45) -- on accept, either side may send CZ_DUEL_START_SEND (symmetric).</summary>
public sealed class DuelAnswerHandler(IDuelService duelService, ILogger<DuelAnswerHandler>? logger = null)
    : IInlinePacketHandler<DuelAnswerRequest>
{
    public void Handle(in DuelAnswerRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var targetId = zoneSession.CharacterId!.Value;

        logger?.LogDebug("Duel answer received: session {SessionId} character {CharacterId} answer {Answer}",
            session.SessionId, targetId, packet.Answer);

        if (packet.Answer is not (0 or 1 or 2))
        {
            // Wire-contract violation, not a business rejection -- the answer code is outside the documented
            // 0/1/2 set (DuelAnswerRequestTests's own coverage), so this is silently dropped exactly as
            // before; only the visibility changed.
            logger?.LogInformation(
                "Duel answer rejected: session {SessionId} character {CharacterId} sent out-of-range answer code {Answer}",
                session.SessionId, targetId, packet.Answer);
            return;
        }

        duelService.Answer(targetId, packet.Answer);
    }
}

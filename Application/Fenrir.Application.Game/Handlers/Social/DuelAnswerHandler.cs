using Fenrir.Application.Game.Social.Duel;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>CZ_DUEL_ANSWER_SEND (opcode 45). 0 = accept, 1/2 = refuse, else ignored. On accept EITHER side may now send CZ_DUEL_START_SEND (<see cref="DuelRegistry" />'s own remarks on symmetric acceptance).</summary>
public sealed class DuelAnswerHandler(ZoneRegistry zones, DuelRegistry duels) : IInlinePacketHandler<DuelAnswerRequest>
{
    public void Handle(in DuelAnswerRequest packet, IPacketSession session)
    {
        if (packet.Answer is not (0 or 1 or 2))
            return;

        var zoneSession = (ZoneClientSession)session;
        var targetId = zoneSession.CharacterId!.Value;

        if (!duels.TryAnswer(targetId, packet.Answer == 0, out var challengerId))
            return;

        if (zones.TryGetPlayer(challengerId, out var challenger))
            challenger.Session.Send(new DuelAnswerResponse { Answer = packet.Answer });
    }
}

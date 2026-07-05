using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>CZ_DUEL_ASK_SEND (opcode 43) -- map 124 (scripted-duel server) always refuses immediately.</summary>
public sealed class DuelAskHandler(IDuelService duelService) : IInlinePacketHandler<DuelChallengeRequest>
{
    public void Handle(in DuelChallengeRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var challengerId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(challengerId, out var challenger) || challenger is null)
            return;

        switch (duelService.Ask(zone, challenger, packet.AvatarName, packet.Sort))
        {
            case DuelAskResultKind.MapForbidden:
                session.Send(new DuelAnswerResponse { Answer = 3 });
                return;
            case DuelAskResultKind.TargetNotFound:
                session.Send(new DuelAnswerResponse { Answer = 4 });
                return;
            case DuelAskResultKind.TribeMismatch:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case DuelAskResultKind.ChallengerBusy:
                session.Send(new DuelAnswerResponse { Answer = 3 });
                return;
            case DuelAskResultKind.TargetBusy:
                session.Send(new DuelAnswerResponse { Answer = 5 });
                return;
        }
    }
}

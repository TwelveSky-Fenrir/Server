using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>CZ_DUEL_ASK_SEND (opcode 43) -- map 124 (scripted-duel server) always refuses immediately.</summary>
public sealed class DuelAskHandler(IDuelService duelService, ILogger<DuelAskHandler>? logger = null)
    : IInlinePacketHandler<DuelChallengeRequest>
{
    public void Handle(in DuelChallengeRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var challengerId = zoneSession.CharacterId!.Value;

        logger?.LogDebug(
            "Duel ask received: session {SessionId} character {CharacterId} -> target {TargetAvatarName} (sort {Sort})",
            session.SessionId, challengerId, packet.AvatarName, packet.Sort);

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
            case DuelAskResultKind.ChallengerAlreadyDueling:
                // A desynced/leaked is-dueling flag on the requester's own side -- not an ordinary "busy"
                // rejection, see DuelAskResultKind.ChallengerAlreadyDueling's own remarks.
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

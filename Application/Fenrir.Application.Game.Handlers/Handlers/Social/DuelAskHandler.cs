using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class DuelAskHandler(IDuelService duelService, ILogger<DuelAskHandler>? logger = null)
    : IAsyncPacketHandler<DuelChallengeRequest>
{
    public async ValueTask HandleAsync(DuelChallengeRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
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

        var result = await duelService.AskAsync(zone, challenger, packet.AvatarName, packet.Sort, cancellationToken)
            .ConfigureAwait(false);

        switch (result)
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
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case DuelAskResultKind.ChallengerBusy:
                session.Send(new DuelAnswerResponse { Answer = 3 });
                return;
            case DuelAskResultKind.TargetBusy:
                session.Send(new DuelAnswerResponse { Answer = 5 });
                return;
            case DuelAskResultKind.SentCrossShard:
                return;
        }
    }
}

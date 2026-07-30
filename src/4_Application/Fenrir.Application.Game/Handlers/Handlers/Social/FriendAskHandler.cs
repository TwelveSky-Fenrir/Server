using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class FriendAskHandler(IFriendService friendService, ILogger<FriendAskHandler> logger)
    : IAsyncPacketHandler<FriendRequest>
{
    public async ValueTask HandleAsync(FriendRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        logger.LogDebug("FriendAsk: session {SessionId} character {CharacterId} target {TargetAvatarName}",
            session.SessionId, zoneSession.CharacterId, packet.AvatarName);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var askerId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(askerId, out var asker) || asker is null)
            return;

        var result = await friendService.AskAsync(zone, asker, packet.AvatarName, cancellationToken)
            .ConfigureAwait(false);

        switch (result)
        {
            case FriendAskResultKind.MapForbidden:
                return;
            case FriendAskResultKind.TargetNotFound:
                session.Send(new FriendAnswerResponse { Answer = 4 });
                return;
            case FriendAskResultKind.AlreadyFriendOrFull:
            case FriendAskResultKind.TribeMismatch:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case FriendAskResultKind.AskerBusy:
                session.Send(new FriendAnswerResponse { Answer = 3 });
                return;
            case FriendAskResultKind.TargetBusy:
                session.Send(new FriendAnswerResponse { Answer = 5 });
                return;
            case FriendAskResultKind.SentCrossShard:
                return;
        }
    }
}

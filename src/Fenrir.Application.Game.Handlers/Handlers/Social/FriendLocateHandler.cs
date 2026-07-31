using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class FriendLocateHandler(IFriendService friendService, ILogger<FriendLocateHandler> logger)
    : IAsyncPacketHandler<FriendLocateRequest>
{
    public async ValueTask HandleAsync(FriendLocateRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        logger.LogDebug("FriendLocate: session {SessionId} character {CharacterId} slot {Index}", session.SessionId,
            zoneSession.CharacterId, packet.Index);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var asker) || asker is null)
            return;

        var result = await friendService.LocateAsync(asker, packet.Index, cancellationToken).ConfigureAwait(false);

        switch (result.Kind)
        {
            case FriendLocateResultKind.IndexOutOfRange:
                return;
            case FriendLocateResultKind.SlotEmpty:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case FriendLocateResultKind.Found:
                session.Send(new FriendLocateResponse { Index = packet.Index, ZoneNumber = result.ZoneNumber });
                return;
        }
    }
}

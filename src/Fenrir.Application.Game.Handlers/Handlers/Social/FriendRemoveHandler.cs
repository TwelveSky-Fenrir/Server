using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class FriendRemoveHandler(IFriendService friendService, ILogger<FriendRemoveHandler> logger)
    : IAsyncPacketHandler<FriendRemoveRequest>
{
    public async ValueTask HandleAsync(FriendRemoveRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        logger.LogDebug("FriendRemove: session {SessionId} character {CharacterId} slot {Index}", session.SessionId,
            zoneSession.CharacterId, packet.Index);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        var result = await friendService.RemoveAsync(state, packet.Index, cancellationToken);

        switch (result)
        {
            case FriendRemoveResultKind.IndexOutOfRange:
                return;
            case FriendRemoveResultKind.SlotEmpty:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case FriendRemoveResultKind.Removed:
                session.Send(new FriendRemoveResponse { Index = packet.Index });
                return;
        }
    }
}

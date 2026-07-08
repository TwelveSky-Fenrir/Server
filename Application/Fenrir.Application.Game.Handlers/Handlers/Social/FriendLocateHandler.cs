using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>
///     CZ_FRIEND_FIND_SEND (opcode 57) -- friend lookup is process-wide (unlike FriendAsk's own-zone-only
///     search), falling back to the cross-shard character-location directory on a same-shard miss. Async
///     (not inline): the fallback is an awaited DB call on the miss branch, and both handler kinds already
///     run on the per-connection session loop, never the zone tick.
/// </summary>
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

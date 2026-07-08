using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>
///     CZ_FRIEND_MAKE_SEND (opcode 56) -- one-directional: only the caller's own list gains an entry; the
///     other side must separately send its own CZ_FRIEND_MAKE_SEND.
/// </summary>
/// <remarks>
///     <see cref="PlayerRuntimeState.Friends" /> is mutated directly (not via ZoneCommand): safe since
///     self-directed, but must stay a <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}" />
///     since <c>Zone.HandleEnter</c> enumerates it concurrently during zone transfer.
/// </remarks>
public sealed class FriendAddHandler(IFriendService friendService, ILogger<FriendAddHandler> logger)
    : IAsyncPacketHandler<FriendAddRequest>
{
    public async ValueTask HandleAsync(FriendAddRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        logger.LogDebug("FriendAdd: session {SessionId} character {CharacterId} slot {Index}", session.SessionId,
            zoneSession.CharacterId, packet.Index);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        var result = await friendService.AddAsync(state, packet.Index, cancellationToken);

        switch (result.Kind)
        {
            case FriendAddResultKind.InvalidSlot:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case FriendAddResultKind.NoPendingAccept:
                return;
            case FriendAddResultKind.Added:
                session.Send(new FriendAddResponse { Index = packet.Index, AvatarName = result.OtherName });
                return;
        }
    }
}

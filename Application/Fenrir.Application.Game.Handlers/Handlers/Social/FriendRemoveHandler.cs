using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>
///     CZ_FRIEND_DELETE_SEND (opcode 58) -- out-of-range slot is a silent no-op, an in-range but already-empty
///     slot ⇒ Quit(); otherwise clears then mirrors <see cref="PlayerRuntimeState.Friends" />.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:9286-9301 (FriendRemove/CZ_FRIEND_DELETE_SEND handler
///     body) -- silent no-op if the slot index is out of range, disconnect if the slot is in range but
///     already empty. Mirrors the FriendLocate (opcode 57) split in <see cref="FriendLocateHandler" />.
/// </remarks>
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

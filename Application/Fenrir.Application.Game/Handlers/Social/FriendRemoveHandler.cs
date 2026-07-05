using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Data.Social;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_FRIEND_DELETE_SEND (opcode 58) -- empty slot ⇒ Quit(); otherwise clears then mirrors
///     <see cref="PlayerRuntimeState.Friends" />.
/// </summary>
public sealed class FriendRemoveHandler(IFriendService friendService) : IAsyncPacketHandler<FriendRemoveRequest>
{
    public async ValueTask HandleAsync(FriendRemoveRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        var result = await friendService.RemoveAsync(state, packet.Index, cancellationToken);

        if (result == FriendRemoveResultKind.InvalidSlot)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        session.Send(new FriendRemoveResponse { Index = packet.Index });
    }
}

using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Social;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>CZ_FRIEND_DELETE_SEND (opcode 58) -- an empty slot ⇒ Quit() (verified); otherwise clears the slot durably then mirrors <see cref="PlayerRuntimeState.Friends" />.</summary>
public sealed class FriendRemoveHandler(FriendRepository repository) : IAsyncPacketHandler<FriendRemoveRequest>
{
    private const int MaxFriends = 10;

    public async ValueTask HandleAsync(FriendRemoveRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        if (packet.Index is < 0 or >= MaxFriends || !state.Friends.ContainsKey((byte)packet.Index))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var slot = (byte)packet.Index;
        await repository.RemoveAsync(characterId, slot, cancellationToken);
        state.Friends.TryRemove(slot, out _);

        session.Send(new FriendRemoveResponse { Index = packet.Index });
    }
}

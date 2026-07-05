using Fenrir.Application.Game.Social.Friends;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Social;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_FRIEND_MAKE_SEND (opcode 56) -- one-directional: only the caller's own list gains an entry; the
///     other side must separately send its own CZ_FRIEND_MAKE_SEND.
/// </summary>
/// <remarks>
///     <see cref="PlayerRuntimeState.Friends" /> is mutated directly (not via ZoneCommand): safe since
///     self-directed, but must stay a <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}" />
///     since <c>Zone.HandleEnter</c> enumerates it concurrently during zone transfer.
/// </remarks>
public sealed class FriendAddHandler(ZoneRegistry zones, FriendRegistry friends, IFriendRepository repository)
    : IAsyncPacketHandler<FriendAddRequest>
{
    private const int MaxFriends = 10;

    public async ValueTask HandleAsync(FriendAddRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        if (packet.Index is < 0 or >= MaxFriends || state.Friends.ContainsKey((byte)packet.Index))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (!friends.TryConsumeAccepted(characterId, out var otherId))
            return;

        var slot = (byte)packet.Index;
        await repository.AddAsync(characterId, slot, otherId, cancellationToken);

        state.Friends[slot] = otherId;

        var otherName = zones.TryGetPlayer(otherId, out var other) ? other.Name : "";
        session.Send(new FriendAddResponse { Index = packet.Index, AvatarName = otherName });
    }
}

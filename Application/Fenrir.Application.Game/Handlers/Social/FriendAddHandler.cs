using Fenrir.Application.Game.Social.Friends;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Data.Social;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_FRIEND_MAKE_SEND (opcode 56). Out-of-bounds slot or an already-occupied one ⇒ Quit(); requires
///     an accepted-but-not-yet-consumed answer (<see cref="FriendRegistry.TryConsumeAccepted" />).
///     ONE-DIRECTIONAL: only the calling character's own list gains an entry -- the other side must
///     separately send its own CZ_FRIEND_MAKE_SEND (game.CharacterFriends' own header).
/// </summary>
/// <remarks>
///     Deviates from the single-writer-via-<c>ZoneCommand</c> pattern: <see cref="PlayerRuntimeState.Friends" />
///     is mutated directly from this request thread. Safe because it's always self-directed (only the
///     caller's own state), and because a zone-transfer handoff (<c>Zone.HandleEnter</c>) concurrently
///     enumerates the same instance on the target zone's tick thread, so <see cref="PlayerRuntimeState.Friends" />
///     is a <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}" /> rather than a plain
///     <c>Dictionary</c>. Not the CAS-style pattern <c>MonsterEntity.TakeDamage</c> uses -- no atomic
///     decision rides on this data, just thread-safety for the enumerator.
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

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
///     KNOWN DEVIATION from the strict single-writer invariant, documented rather than hidden: the
///     durable SQL write happens first, but <see cref="PlayerRuntimeState.Friends" /> is then mutated
///     DIRECTLY from this request thread rather than via a posted <c>ZoneCommand</c> the zone's own tick
///     applies. Narrower and lower-risk than it sounds -- always self-directed (a character only ever
///     touches its OWN <see cref="PlayerRuntimeState" /> here, never another's). CORRECTION (review finding,
///     Phase C/V6): an earlier draft of this comment claimed "nothing on the zone's tick currently reads
///     Friends" -- that is FALSE. A zone-transfer handoff (<c>ZoneTransfer.CreateEnterData</c> carries the
///     SAME <see cref="PlayerRuntimeState.Friends" /> instance across; <c>Zone.HandleEnter</c> enumerates it
///     on the TARGET zone's own tick thread) is a real concurrent reader, so a request-thread Add/Remove
///     racing that enumeration was a genuine crash risk on a plain <c>Dictionary</c>. Fixed at the source:
///     <see cref="PlayerRuntimeState.Friends" /> is now a <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}" />,
///     which tolerates a concurrent enumerator without throwing (it may simply miss or double-see an
///     in-flight mutation, an acceptable eventual-consistency gap for a friends list). This is still NOT the
///     same CAS-style exception pattern <c>MonsterEntity.TakeDamage</c>/<c>Zone.TryClaimGroundItem</c> use --
///     no atomic compare-and-set decision rides on this data -- just a plain thread-safe collection.
/// </remarks>
public sealed class FriendAddHandler(ZoneRegistry zones, FriendRegistry friends, FriendRepository repository)
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

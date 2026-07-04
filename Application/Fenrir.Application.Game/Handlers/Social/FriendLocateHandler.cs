using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_FRIEND_FIND_SEND (opcode 57). Out-of-bounds index ⇒ silent return; an EMPTY slot ⇒ Quit()
///     (verified, contract's own distinction from the out-of-bounds case). Resolved process-wide (this
///     IS one of the two directory-backed lookups, alongside whisper -- see
///     <see cref="World.ZoneRegistry.TryGetPlayerByName" />'s own remarks); <c>ZoneNumber = -1</c> if the
///     friend is offline OR its tribe no longer matches the caller's own (S04_MyWork02.cpp:9277-9280).
/// </summary>
public sealed class FriendLocateHandler(ZoneRegistry zones) : IInlinePacketHandler<FriendLocateRequest>
{
    private const int MaxFriends = 10;

    public void Handle(in FriendLocateRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (packet.Index is < 0 or >= MaxFriends)
            return;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var asker) || asker is null)
            return;

        if (!asker.Friends.TryGetValue((byte)packet.Index, out var friendId))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var zoneNumber = -1;
        if (zones.TryGetPlayer(friendId, out var friend) && friend.Tribe == asker.Tribe)
            zoneNumber = friend.MapId;

        session.Send(new FriendLocateResponse { Index = packet.Index, ZoneNumber = zoneNumber });
    }
}

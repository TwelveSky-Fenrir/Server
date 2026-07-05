using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>CZ_FRIEND_FIND_SEND (opcode 57) -- friend lookup is process-wide (unlike FriendAsk's own-zone-only search).</summary>
public sealed class FriendLocateHandler(IFriendService friendService) : IInlinePacketHandler<FriendLocateRequest>
{
    public void Handle(in FriendLocateRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var asker) || asker is null)
            return;

        var result = friendService.Locate(asker, packet.Index);

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

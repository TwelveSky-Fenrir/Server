using Fenrir.Application.Game.Social.Chat;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Chat;

/// <summary>
///     CZ_SECRET_CHAT_SEND (opcode 39). Cross-tribe gating is commented out in this fork -- inter-tribe
///     whispers pass. Target resolved process-wide (unlike duel/trade/friend/mentor/party's same-zone-only
///     lookup, see <see cref="ZoneRegistry.TryGetPlayerAndZoneByName" />). No mute gate applies here.
/// </summary>
public sealed class WhisperHandler(ZoneRegistry zones) : IInlinePacketHandler<WhisperRequest>
{
    // Socket is a reference-type array; a bare `default` would leave it null and crash the wire writer.
    private static readonly ItemLinkInfo EmptyLink = new() { Index = 0, Activity = 0, Value = 0, Socket = new int[3] };

    public void Handle(in WhisperRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (ChatRouter.IsContentEmpty(packet.Content) || string.IsNullOrEmpty(packet.AvatarName))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var sender) || sender is null)
            return;

        if (string.Equals(sender.Name, packet.AvatarName, StringComparison.OrdinalIgnoreCase))
            return;

        if (!zones.TryGetPlayerAndZoneByName(packet.AvatarName, out var target, out var targetZone))
        {
            session.Send(new WhisperResponse
            {
                Result = 1,
                ZoneNumber = 0,
                AvatarName = packet.AvatarName,
                Content = "",
                AuthType = 0,
                Link = EmptyLink
            });
            return;
        }

        // Echo to the sender (Result=0) before delivering to the target (Result=3) -- legacy ordering.
        session.Send(new WhisperResponse
        {
            Result = 0,
            ZoneNumber = targetZone.MapId,
            AvatarName = target.Name,
            Content = packet.Content,
            AuthType = 0,
            Link = packet.Link
        });

        target.Session.Send(new WhisperResponse
        {
            Result = 3,
            ZoneNumber = 0,
            AvatarName = sender.Name,
            Content = packet.Content,
            AuthType = 0,
            Link = packet.Link
        });
    }
}

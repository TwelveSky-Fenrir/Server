using Fenrir.Application.Game.Social.Chat;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Chat;

/// <summary>
///     CZ_SECRET_CHAT_SEND (opcode 39, contracts/02_chat_notices.md). Empty name/content ⇒ anti-fuzzing
///     Quit(); whispering yourself ⇒ silent return; cross-tribe gating (wire Result=2) is commented out
///     in this fork -- inter-tribe whispers pass. Target resolved process-wide (verified: ts25playuser is
///     the one directory service that IS cross-zone, unlike duel/trade/friend/mentor/party's own
///     same-zone-only <c>SearchAvatar</c> -- see <see cref="ZoneRegistry.TryGetPlayerAndZoneByName" />'s
///     own remarks). No mute gate is documented for this channel (contract's own note omits "vérif
///     mute" for CZ 39, unlike local/shout/guild/tribe/world chat) -- preserved as-is (D8).
/// </summary>
public sealed class WhisperHandler(ZoneRegistry zones) : IInlinePacketHandler<WhisperRequest>
{
    /// <summary>
    ///     "Zeroed when the builder is called with NULL" (WhisperResponse.Link's own remarks) -- <c>Socket</c> is a
    ///     reference-type array, so a bare <c>default</c> would leave it null and crash the wire writer.
    /// </summary>
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
            return; // whispering yourself -- silent return, not an error reply

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

        // Echo to the sender (Result=0, target's zone number) THEN deliver to the target (Result=3, sender's name) --
        // matches the legacy's own "echo, then relay" ordering (contracts/02_chat_notices.md).
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

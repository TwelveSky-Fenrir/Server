using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

/// <summary>
///     CZ_PARTY_CHAT_SEND (opcode 68). The outgoing link is always zeroed -- the legacy decodes the
///     incoming item link but never relays it, so it is decoded here and then deliberately discarded.
/// </summary>
public sealed class PartyChatHandler(IPartyChatService partyChatService, ILogger<PartyChatHandler> logger)
    : IInlinePacketHandler<PartyChatRequest>
{
    public void Handle(in PartyChatRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        // Message content itself is never logged (chat text is sensitive, same posture as PacketLog's own
        // deliberate omission of payload bytes) -- only metadata identifying who sent something and how long it was.
        logger.LogDebug(
            "PartyChat: session {SessionId} character {CharacterId} content length {ContentLength}",
            session.SessionId, zoneSession.CharacterId, packet.Content.Length);

        if (ChatRouter.IsContentEmpty(packet.Content))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var sender) || sender is null)
            return;

        partyChatService.TrySendChat(sender, packet.Content);
    }
}

using Fenrir.Application.Game.Handlers.Chat.Services;
using Fenrir.Application.Game.Social.Chat;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Chat;

/// <summary>
///     CZ_PARTY_CHAT_SEND (opcode 68). The outgoing link is always zeroed -- the legacy decodes the
///     incoming item link but never relays it, so it is decoded here and then deliberately discarded.
/// </summary>
public sealed class PartyChatHandler(IPartyChatService partyChatService) : IInlinePacketHandler<PartyChatRequest>
{
    public void Handle(in PartyChatRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

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

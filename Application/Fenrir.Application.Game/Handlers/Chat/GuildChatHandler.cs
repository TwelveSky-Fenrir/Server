using Fenrir.Application.Game.Handlers.Chat.Services;
using Fenrir.Application.Game.Social.Chat;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Chat;

/// <summary>
///     CZ_GUILD_CHAT_SEND (opcode 77). Guildless/muted senders are silently dropped, not disconnected.
///     Unlike party chat, the item link here is genuinely relayed to every guild member.
/// </summary>
public sealed class GuildChatHandler(IGuildChatService guildChatService) : IInlinePacketHandler<GuildChatRequest>
{
    public void Handle(in GuildChatRequest packet, IPacketSession session)
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

        guildChatService.TrySendChat(sender, packet.Content, packet.Link);
    }
}

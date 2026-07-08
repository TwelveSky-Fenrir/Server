using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

/// <summary>
///     CZ_GUILD_CHAT_SEND (opcode 77). Guildless/muted senders are silently dropped, not disconnected.
///     Unlike party chat, the item link here is genuinely relayed to every guild member.
/// </summary>
public sealed class GuildChatHandler(IGuildChatService guildChatService, ILogger<GuildChatHandler>? logger = null)
    : IInlinePacketHandler<GuildChatRequest>
{
    public void Handle(in GuildChatRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        logger?.LogDebug(
            "Session {SessionId}: CZ_GUILD_CHAT_SEND received (character {CharacterId}, content length {ContentLength})",
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

        guildChatService.TrySendChat(sender, packet.Content, packet.Link);
    }
}

using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Shared;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class TribeChatService(ILogger<TribeChatService> logger) : ITribeChatService
{
    public bool TryPostChat(Zone zone, PlayerRuntimeState sender, string content, ItemLinkInfo link)
    {
        if (sender.IsMuted)
        {
            // A moderation action (mute) actively being enforced -- worth surfacing by default, mirroring
            // GuildChatService's own muted-drop treatment.
            logger.LogInformation("Character {CharacterId} tribe chat dropped: caller is muted", sender.CharacterId);
            return false;
        }

        zone.PostChatCommand(new ChatZoneCommand
        {
            SenderCharacterId = sender.CharacterId,
            Kind = ChatBroadcastKind.Tribe,
            Content = content,
            Link = link
        });

        logger.LogDebug("Character {CharacterId} posted tribe chat in zone {MapId} ({ContentLength} chars)",
            sender.CharacterId, zone.MapId, content.Length);

        return true;
    }
}

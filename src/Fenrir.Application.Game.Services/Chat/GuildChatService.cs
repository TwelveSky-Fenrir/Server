using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class GuildChatService(
    ZoneRegistry zones,
    IGuildTribeBroadcastRelayQueue relay,
    IOptions<GameServerOptions> options,
    ILogger<GuildChatService> logger) : IGuildChatService
{
    public bool TrySendChat(PlayerRuntimeState sender, string content, ItemLinkInfo link)
    {
        if (sender.GuildId is not { } guildId)
        {
            logger.LogDebug("Character {CharacterId} guild chat dropped: caller has no guild", sender.CharacterId);
            return false;
        }

        if (sender.IsMuted)
        {
            logger.LogInformation("Character {CharacterId} guild chat dropped: caller is muted", sender.CharacterId);
            return false;
        }

        var response = new GuildChatResponse { AvatarName = sender.Name, Content = content, Link = link };

        var recipientCount = 0;
        foreach (var target in zones.Zones)
        foreach (var recipient in target.Players)
            if (recipient.GuildId == guildId)
            {
                recipient.Session.Send(response);
                recipientCount++;
            }

        relay.Enqueue(new GuildTribeBroadcastRelayEntry(
            GuildTribeBroadcastKind.GuildChat,
            options.Value.ShardId,
            guildId,
            null,
            0,
            sender.Name,
            content,
            true,
            link.Index,
            link.Activity,
            link.Value,
            link.Socket[0],
            link.Socket[1],
            link.Socket[2])
        {
            SourceCharacterId = sender.CharacterId
        });

        logger.LogDebug(
            "Character {CharacterId} sent guild chat to guild {GuildId} ({RecipientCount} same-shard recipients, {ContentLength} chars)",
            sender.CharacterId, guildId, recipientCount, content.Length);

        return true;
    }
}

using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class GuildAnnouncementService(
    ZoneRegistry zones,
    IGuildTribeBroadcastRelayQueue relay,
    IOptions<GameServerOptions> options,
    ILogger<GuildAnnouncementService> logger) : IGuildAnnouncementService
{
    public bool TrySendAnnouncement(PlayerRuntimeState sender, string content)
    {
        if (sender.IsMuted)
        {
            logger.LogInformation("Character {CharacterId} guild announcement dropped: caller is muted",
                sender.CharacterId);
            return false;
        }

        if (sender.GuildId is not { } guildId || !GuildRoleCodec.IsMaster(sender.GuildRoleDb))
        {
            logger.LogDebug(
                "Character {CharacterId} guild announcement rejected: caller is not the guild master (guild {GuildId})",
                sender.CharacterId, sender.GuildId);
            return false;
        }

        var response = new GuildAnnouncementResponse { AvatarName = sender.Name, Content = content };

        var recipientCount = 0;
        foreach (var target in zones.Zones)
        foreach (var recipient in target.Players)
            if (recipient.GuildId == guildId)
            {
                recipient.Session.Send(response);
                recipientCount++;
            }

        relay.Enqueue(new GuildTribeBroadcastRelayEntry(
            GuildTribeBroadcastKind.GuildAnnouncement,
            options.Value.ShardId,
            guildId,
            null,
            0,
            sender.Name,
            content,
            false,
            null,
            null,
            null,
            null,
            null,
            null)
        {
            SourceCharacterId = sender.CharacterId
        });

        logger.LogInformation(
            "Character {CharacterId} broadcast a guild announcement to guild {GuildId} ({RecipientCount} same-shard recipients, {ContentLength} chars)",
            sender.CharacterId, guildId, recipientCount, content.Length);

        return true;
    }
}

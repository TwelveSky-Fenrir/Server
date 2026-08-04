using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class TribeAnnouncementService(
    ZoneRegistry zones,
    IGuildTribeBroadcastRelayQueue relay,
    IOptions<GameServerOptions> options,
    ILogger<TribeAnnouncementService> logger) : ITribeAnnouncementService
{
    public bool TrySendAnnouncement(PlayerRuntimeState sender, string content)
    {
        if (sender.IsMuted)
        {
            logger.LogInformation("Character {CharacterId} tribe announcement dropped: caller is muted",
                sender.CharacterId);
            return false;
        }

        if (sender.TribeRole == 0)
        {
            logger.LogDebug(
                "Character {CharacterId} tribe announcement rejected: caller holds no tribe role (tribe {Tribe})",
                sender.CharacterId, sender.Tribe);
            return false;
        }

        var response = new TribeAnnouncementResponse
            { TribeRole = sender.TribeRole, AvatarName = sender.Name, Content = content };

        var recipientCount = 0;
        foreach (var target in zones.Zones)
        foreach (var recipient in target.Players)
            if (recipient.Tribe == sender.Tribe)
            {
                recipient.Session.Send(response);
                recipientCount++;
            }

        relay.Enqueue(new GuildTribeBroadcastRelayEntry(
            GuildTribeBroadcastKind.TribeAnnouncement,
            options.Value.ShardId,
            null,
            sender.Tribe,
            sender.TribeRole,
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
            "Character {CharacterId} (tribe role {TribeRole}) broadcast a tribe announcement to tribe {Tribe} ({RecipientCount} same-shard recipients, {ContentLength} chars)",
            sender.CharacterId, sender.TribeRole, sender.Tribe, recipientCount, content.Length);

        return true;
    }
}

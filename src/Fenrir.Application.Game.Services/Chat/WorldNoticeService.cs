using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class WorldNoticeService(
    ZoneRegistry zones,
    IGuildTribeBroadcastRelayQueue relay,
    IOptions<GameServerOptions> options,
    ILogger<WorldNoticeService> logger) : IWorldNoticeService
{
    public void Broadcast(string content)
    {
        var safeContent = ChatRouter.SafeContent(content);

        var response = new GlobalAnnouncementResponse { Content = safeContent };

        var recipientCount = 0;
        foreach (var zone in zones.Zones)
        foreach (var recipient in zone.Players)
        {
            if (recipient.IsMovingZone)
                continue;

            recipient.Session.Send(response);
            recipientCount++;
        }

        relay.Enqueue(new GuildTribeBroadcastRelayEntry(
            GuildTribeBroadcastKind.GlobalAnnouncement,
            options.Value.ShardId,
            null,
            null,
            0,
            string.Empty,
            safeContent,
            false,
            null,
            null,
            null,
            null,
            null,
            null));

        logger.LogInformation(
            "System world notice broadcast cluster-wide ({RecipientCount} same-shard recipients, {ContentLength} chars): {Content}",
            recipientCount, safeContent.Length, safeContent);
    }
}

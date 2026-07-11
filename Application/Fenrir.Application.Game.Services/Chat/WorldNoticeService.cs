using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class WorldNoticeService(ZoneRegistry zones, ILogger<WorldNoticeService> logger) : IWorldNoticeService
{
    public void Broadcast(string content)
    {
        var response = new GlobalAnnouncementResponse { Content = content };

        var recipientCount = 0;
        foreach (var zone in zones.Zones)
        foreach (var recipient in zone.Players)
        {
            if (recipient.IsMovingZone)
                continue;

            recipient.Session.Send(response);
            recipientCount++;
        }

        logger.LogInformation(
            "System world notice broadcast shard-wide ({RecipientCount} recipients, {ContentLength} chars): {Content}",
            recipientCount, content.Length, content);
    }
}

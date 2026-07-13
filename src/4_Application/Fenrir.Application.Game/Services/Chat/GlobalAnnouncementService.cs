using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class GlobalAnnouncementService(ZoneRegistry zones, ILogger<GlobalAnnouncementService> logger)
    : IGlobalAnnouncementService
{
    public void TryAnnounce(ZoneClientSession zoneSession, string content)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Basic))
        {
            logger.LogDebug(
                "Character {CharacterId} global announcement ignored: caller does not meet GM tier {Tier}",
                zoneSession.CharacterId, GmCommandTier.Basic);
            return;
        }

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
            "Character {CharacterId} broadcast a global announcement cluster-wide ({RecipientCount} recipients, {ContentLength} chars)",
            zoneSession.CharacterId, recipientCount, content.Length);
    }
}

using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Gm;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.Chat;

public sealed class GlobalAnnouncementService(
    ZoneRegistry zones,
    IGuildTribeBroadcastRelayQueue relay,
    IEventLogQueue eventLogQueue,
    IOptions<GameServerOptions> options,
    ILogger<GlobalAnnouncementService> logger) : IGlobalAnnouncementService
{
    public void TryAnnounce(IZoneSession zoneSession, string content)
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

        relay.Enqueue(new GuildTribeBroadcastRelayEntry(
            GuildTribeBroadcastKind.GlobalAnnouncement,
            options.Value.ShardId,
            null,
            null,
            0,
            string.Empty,
            content,
            false,
            null,
            null,
            null,
            null,
            null,
            null));

        Audit(zoneSession, GmCommandCatalog.OutcomeExecuted,
            $"Command=NOTICE;RecipientCount={recipientCount};Content={content}");

        logger.LogInformation(
            "Character {CharacterId} broadcast a global announcement cluster-wide ({RecipientCount} same-shard recipients, {ContentLength} chars)",
            zoneSession.CharacterId, recipientCount, content.Length);
    }

    private void Audit(IZoneSession zoneSession, byte outcome, string payload)
    {
        if (!eventLogQueue.Enqueue(new EventLogEntryTvp(GmCommandCatalog.GlobalAnnouncementAudit,
                (byte)EventLogCategory.GmAction, zoneSession.AccountId, zoneSession.CharacterId, null, null, null,
                null, null, null, null, outcome, payload, DateTime.UtcNow)))
            logger.LogWarning(
                "game.EventLog write-behind queue full: dropped GM global-announcement audit row for character {CharacterId}",
                zoneSession.CharacterId);
    }
}

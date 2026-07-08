using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Chat;

/// <summary>
///     See <see cref="IWorldNoticeService" />'s own remarks for the full behavior contract. Deliberately
///     shares its shard-wide fan-out shape with <see cref="GlobalAnnouncementService" /> -- both ultimately
///     relay the same legacy sort (102) to the same client-facing wire response
///     (<see cref="GlobalAnnouncementResponse" />, ZCP_GENERAL_NOTICE_RECV opcode 20) -- but owns none of
///     that service's op17 permission gate, since callers here are system-triggered, not player commands.
/// </summary>
public sealed class WorldNoticeService(ZoneRegistry zones, ILogger<WorldNoticeService> logger) : IWorldNoticeService
{
    public void Broadcast(string content)
    {
        var response = new GlobalAnnouncementResponse { Content = content };

        var recipientCount = 0;
        foreach (var zone in zones.Zones)
        foreach (var recipient in zone.Players)
        {
            // Same combined readiness+non-mid-transfer guard as GlobalAnnouncementService -- every player
            // tracked in Zone.Players is already "ready" by construction, so the remaining half of the guard
            // is excluding an in-flight cross-shard transfer.
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

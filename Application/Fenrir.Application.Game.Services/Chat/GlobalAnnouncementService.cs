using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Chat;

/// <summary>
///     See <see cref="IGlobalAnnouncementService" />'s own remarks for the full behavior contract.
///     Citations: Server/ts25zone/S04_MyWork02.cpp:1924-1943 (permission gate, no sanitization, no
///     emptiness check -- unlike every sibling opcode in this cluster, this one relays byte-for-byte) ;
///     Server/Header/Protocol/ZONE.h:1431, :443-446 (outbound opcode 20 ZCP_GENERAL_NOTICE_RECV,
///     <see cref="GlobalAnnouncementResponse" />) ; Server/ts25zone/S04_MyWork04.cpp:25-36 (receiving-zone
///     delivery: readiness AND non-mid-transfer, checked together -- unlike
///     <see cref="IWorldChatService" />'s deliberately readiness-only fan-out, see that interface's own
///     remarks, this is a real, verified difference between the two cluster-wide opcodes, not an
///     oversight, so this service filters on <see cref="PlayerRuntimeState.IsMovingZone" /> where
///     <c>WorldChatService</c> does not).
/// </summary>
public sealed class GlobalAnnouncementService(ZoneRegistry zones, ILogger<GlobalAnnouncementService> logger)
    : IGlobalAnnouncementService
{
    public void TryAnnounce(ZoneClientSession zoneSession, string content)
    {
        // uUserSort < 1 gate (S04_MyWork02.cpp:1924-1943): denied senders observe nothing at all -- no
        // reply, no disconnect (the legacy Quit() call for this branch is commented out, :1926-1930). See
        // IGlobalAnnouncementService's own remarks for why this must never Abort the session.
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
            // S04_MyWork04.cpp:31's combined readiness+non-mid-transfer guard -- every player tracked in
            // Zone.Players is already "ready" by construction (HandleEnter is the only add site), so the
            // remaining half of that guard is excluding an in-flight cross-shard transfer.
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

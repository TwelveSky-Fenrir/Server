using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Data.Abstractions.Game;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

/// <summary>
///     See <see cref="IGmFfaEventStartService" />'s own remarks for the wire-level contract summary. Citations:
///     Server/ts25zone/S04_MyWork04.cpp:1097-1131 (case 333 full handler: tier gate at :1100-1104, idle-state
///     guard at :1105-1110, inline <c>GM_FFASTART_RECV{time}</c> at :1112-1116, countdown default-vs-conversion
///     at :1119-1126, trigger flag at :1128, success result at :1129) ; Server/Header/Protocol/STRUCT.h:590-650,
///     :645 (<c>WORLD_INFO.mZoneFFATypeState</c>, ported as <see cref="ZoneCenterSiegeState.Zone335" />) ;
///     Server/ts25zone/H07_MyGame.h:331-339 (<c>mZone335TypeRemainTime2</c>/<c>mZone335StartCommandCheck</c>,
///     process-local <c>MyGame</c> singleton fields, ported as <see cref="Zone335StartTrigger" /> -- see that
///     class's own remarks for why it is a distinct object from <see cref="ZoneCenterSiegeState" />) ;
///     Server/ts25zone/S07_MyGame01.cpp:10736-10850 (<c>Process_Zone_335_FFA</c>, the autonomous consuming tick
///     -- NOT ported by this type, see its own remarks).
/// </summary>
public sealed class GmFfaEventStartService(
    ZoneCenterSiegeState siegeState,
    Zone335StartTrigger startTrigger,
    IEventLogRepository eventLog,
    ILogger<GmFfaEventStartService> logger) : IGmFfaEventStartService
{
    private const int Sort = 333;
    private const int AcceptedResult = 0;
    private const int RejectedResult = 1;

    /// <summary><see cref="ZoneCenterSiegeState.Zone335" />'s own idle value (<c>ResetZone335</c>'s target).</summary>
    private const int IdlePhase = 0;

    private static readonly TimeSpan DefaultCountdown = TimeSpan.FromMinutes(10);

    public async ValueTask HandleAsync(GmFfaEventStartPayload packet, byte[] data, ZoneClientSession zoneSession,
        CancellationToken cancellationToken)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Elevated))
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Elevated-tier zone-wide FFA-start command (sort {Sort}) without sufficient privilege -- disconnecting, no reply",
                zoneSession.CharacterId, Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var accepted = siegeState.Zone335 == IdlePhase;
        if (accepted)
        {
            var countdown = packet.Time > 0 ? TimeSpan.FromMinutes(packet.Time) : DefaultCountdown;
            startTrigger.Request(SimulationClock.ToWholeLegacyTicks(countdown));
        }

        await eventLog.LogAsync(GmActionEventCodes.FfaEventStart, EventLogCategory.GmAction, zoneSession.AccountId,
            zoneSession.CharacterId, null, null, null, null, null, null, null, accepted ? (byte)1 : (byte)0,
            $"RequestedDurationMinutes={packet.Time};Accepted={accepted}", cancellationToken);

        if (accepted)
            logger.LogInformation(
                "Character {CharacterId} started the Elevated-tier zone-wide FFA event (requested duration {DurationMinutes} minute(s))",
                zoneSession.CharacterId, packet.Time);
        else
            logger.LogInformation(
                "Character {CharacterId} attempted the Elevated-tier zone-wide FFA-start command, but the FFA state was not idle -- rejected",
                zoneSession.CharacterId);

        zoneSession.Send(new GenericActionResponse
        {
            Result = accepted ? AcceptedResult : RejectedResult, Sort = Sort, Data = data, RuneValue = 0
        });
    }
}

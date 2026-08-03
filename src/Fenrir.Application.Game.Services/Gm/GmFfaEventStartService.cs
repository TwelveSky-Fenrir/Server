using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Core.Packets.Shared;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

public sealed class GmFfaEventStartService(
    ZoneCenterSiegeState siegeState,
    Zone335StartTrigger startTrigger,
    IEventLogRepository eventLog,
    ILogger<GmFfaEventStartService> logger) : IGmFfaEventStartService
{
    private const int Sort = 333;
    private const int AcceptedResult = 0;
    private const int RejectedResult = 1;

    private const int IdlePhase = 0;

    private const int MaxBattleDurationMinutes = 1440;

    public async ValueTask HandleAsync(GmFfaEventStartPayload packet, byte[] data, IZoneSession zoneSession,
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
            var requestedMinutes = Math.Clamp(packet.Time, 0, MaxBattleDurationMinutes);
            startTrigger.Request(requestedMinutes > 0
                ? requestedMinutes * 60 * SimulationClock.OneSecondGateLegacyTicks
                : Zone335FfaEventCycleSystem.DefaultBattleDurationLegacyTicks);
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

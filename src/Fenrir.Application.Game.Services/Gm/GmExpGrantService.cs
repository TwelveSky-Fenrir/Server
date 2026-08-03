using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Gm;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

public sealed class GmExpGrantService(
    IEventLogRepository eventLog,
    ILogger<GmExpGrantService> logger) : IGmExpGrantService
{
    private const int Sort = 503;

    private const int AcceptedResult = 0;

    private const byte SuccessOutcome = 1;

    public async ValueTask HandleAsync(GmExpGrantPayload packet, byte[] data, IZoneSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Elevated))
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Elevated-tier grant-experience-to-self command (sort {Sort}) without sufficient privilege -- disconnecting, no reply",
                zoneSession.CharacterId, Sort);
            await eventLog.LogAsync(GmActionEventCodes.ExpGrant, EventLogCategory.GmAction, zoneSession.AccountId,
                zoneSession.CharacterId, null, null, null, null, null, null, null, GmCommandCatalog.OutcomeDenied,
                $"Command=EXP;Sort={Sort};RequiredTier={(short)GmCommandTier.Elevated}", cancellationToken);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (!await zone.PostGmSelfExperienceGrantCommandAndWaitAsync(
                new GmSelfExperienceGrantZoneCommand(state.CharacterId, packet.Type, packet.Exp), cancellationToken))
            logger.LogError(
                "Zone {MapId} GM-experience inbox full: dropped grant-experience-to-self mirror for character {CharacterId} (mode {Mode}, magnitude {Magnitude}) -- reporting accepted regardless, matching legacy's own unconditional success result",
                zone.MapId, state.CharacterId, packet.Type, packet.Exp);

        await eventLog.LogAsync(GmActionEventCodes.ExpGrant, EventLogCategory.GmAction, zoneSession.AccountId,
            zoneSession.CharacterId, null, null, null, null, null, null, null, SuccessOutcome,
            $"Mode={packet.Type};Magnitude={packet.Exp}", cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} applied the Elevated-tier grant-experience-to-self command (mode {Mode}, magnitude {Magnitude})",
            state.CharacterId, packet.Type, packet.Exp);

        zoneSession.Send(new GenericActionResponse
            { Result = AcceptedResult, Sort = Sort, Data = data, RuneValue = 0 });
    }
}

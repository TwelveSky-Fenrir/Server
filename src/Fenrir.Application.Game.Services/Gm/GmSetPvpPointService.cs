using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Gm;
using Fenrir.Core.Packets.Shared;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

public sealed class GmSetPvpPointService(IEventLogRepository eventLog, ILogger<GmSetPvpPointService> logger)
    : IGmSetPvpPointService
{
    private const int Sort = 598;
    private const int AcceptedResult = 0;
    private const int RejectedResult = 1;

    public async ValueTask HandleAsync(GmSetPvpPointPayload packet, byte[] data, IZoneSession zoneSession,
        CancellationToken cancellationToken)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Basic))
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Basic-tier GM-SETPVPPOINT command (sort {Sort}) without sufficient privilege -- disconnecting, no reply",
                zoneSession.CharacterId, Sort);
            await AuditAsync(zoneSession, GmCommandCatalog.OutcomeDenied,
                $"RequiredTier={(short)GmCommandTier.Basic}", cancellationToken);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (packet.DuelSlot is not (1 or 2))
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug(
                    "Character {CharacterId} GM-SETPVPPOINT rejected: invalid duel slot {DuelSlot}",
                    zoneSession.CharacterId, packet.DuelSlot);
            await AuditAsync(zoneSession, GmCommandCatalog.OutcomeRejected, $"DuelSlot={packet.DuelSlot}",
                cancellationToken);
            zoneSession.Send(new GenericActionResponse
                { Result = RejectedResult, Sort = Sort, Data = data, RuneValue = 0 });
            return;
        }

        await AuditAsync(zoneSession, GmCommandCatalog.OutcomeExecuted, $"DuelSlot={packet.DuelSlot}",
            cancellationToken);

        zoneSession.Send(new GenericActionResponse
            { Result = AcceptedResult, Sort = Sort, Data = data, RuneValue = 0 });
    }

    private ValueTask AuditAsync(IZoneSession zoneSession, byte outcome, string detail,
        CancellationToken cancellationToken)
    {
        return eventLog.LogAsync(GmCommandCatalog.SetPvpPointAudit, EventLogCategory.GmAction,
            zoneSession.AccountId, zoneSession.CharacterId, null, null, null, null, null, null, null, outcome,
            $"Command=SETPVPPOINT;Sort={Sort};{detail}", cancellationToken);
    }
}

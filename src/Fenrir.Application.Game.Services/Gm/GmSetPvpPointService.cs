using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Core.Packets.Shared;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

public sealed class GmSetPvpPointService(ILogger<GmSetPvpPointService> logger) : IGmSetPvpPointService
{
    private const int Sort = 598;
    private const int AcceptedResult = 0;
    private const int RejectedResult = 1;

    public ValueTask HandleAsync(GmSetPvpPointPayload packet, byte[] data, IZoneSession zoneSession,
        CancellationToken cancellationToken)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Basic))
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Basic-tier GM-SETPVPPOINT command (sort {Sort}) without sufficient privilege -- disconnecting, no reply",
                zoneSession.CharacterId, Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return ValueTask.CompletedTask;
        }

        if (packet.DuelSlot is not (1 or 2))
        {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug(
                    "Character {CharacterId} GM-SETPVPPOINT rejected: invalid duel slot {DuelSlot}",
                    zoneSession.CharacterId, packet.DuelSlot);
            zoneSession.Send(new GenericActionResponse
                { Result = RejectedResult, Sort = Sort, Data = data, RuneValue = 0 });
            return ValueTask.CompletedTask;
        }

        zoneSession.Send(new GenericActionResponse
            { Result = AcceptedResult, Sort = Sort, Data = data, RuneValue = 0 });
        return ValueTask.CompletedTask;
    }
}

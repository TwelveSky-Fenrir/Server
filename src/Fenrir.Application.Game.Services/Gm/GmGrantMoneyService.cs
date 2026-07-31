using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

public sealed class GmGrantMoneyService(
    IEventLogRepository eventLog,
    ILogger<GmGrantMoneyService> logger) : IGmGrantMoneyService
{
    private const int Sort = 504;

    private const int NotOverwrittenResult = 1;

    private const byte NoOpOutcome = 0;

    public async ValueTask HandleAsync(byte[] data, ZoneClientSession zoneSession, CancellationToken cancellationToken)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Elevated))
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Elevated-tier grant-money command (sort {Sort}) without sufficient privilege -- disconnecting, no reply",
                zoneSession.CharacterId, Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        await eventLog.LogAsync(GmActionEventCodes.GrantMoney, EventLogCategory.GmAction, zoneSession.AccountId,
            zoneSession.CharacterId, null, null, null, null, null, null, null, NoOpOutcome,
            "GM grant-money command invoked; legacy case 504 body is dead/commented-out code (Server/ts25zone/S04_MyWork04.cpp:1013-1033) -- no money granted",
            cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} invoked the Elevated-tier grant-money command -- legacy dead code, no money granted, echoing legacy's own non-success default result",
            zoneSession.CharacterId);

        zoneSession.Send(new GenericActionResponse
            { Result = NotOverwrittenResult, Sort = Sort, Data = data, RuneValue = 0 });
    }
}

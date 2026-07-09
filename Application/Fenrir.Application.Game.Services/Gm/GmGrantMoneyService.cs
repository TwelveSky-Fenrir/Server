using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

/// <summary>
///     See <see cref="IGmGrantMoneyService" />'s own remarks for the wire-level contract summary. Citations:
///     Server/ts25zone/S04_MyWork04.cpp:1006-1035 (case 504 full handler body: tier gate at :1008-1012; the
///     entire remainder, :1013-1033, is <c>//</c>-commented dead code, including the would-be
///     <c>GM_MONEY_RECV{tValue}</c> struct at :1019-1021, the [1,100000000] clamp at :1023, the
///     <c>ProcessForDropItem(DP_GM_TO_WD, 1, r-&gt;tValue, ...)</c> call at :1027, and the
///     <c>GL_601_GM_CREATE_MONEY</c> log call at :1031 ; the case falls to <c>break</c> at :1034-1035 with
///     <c>tResult</c> never reassigned) ; Server/ts25zone/S04_MyWork04.cpp:305 (<c>tResult</c>'s function-entry
///     default of <c>1</c>, never overwritten inside this case).
/// </summary>
public sealed class GmGrantMoneyService(
    IEventLogRepository eventLog,
    ILogger<GmGrantMoneyService> logger) : IGmGrantMoneyService
{
    private const int Sort = 504;

    /// <summary>
    ///     Legacy's own function-entry default for <c>tResult</c> (S04_MyWork04.cpp:305), never reassigned by
    ///     this case's entirely-dead body -- NOT a deliberately-chosen "rejected" sentinel the way
    ///     <c>GmBlockAvatarService.ResultTargetNotFound</c>/<c>GmCreateItemService.RejectedResult</c> are; it is
    ///     reproduced here purely because that is what the compiled legacy binary actually sends back.
    /// </summary>
    private const int NotOverwrittenResult = 1;

    /// <summary>No live effect exists to report success or failure for -- see this type's own class remarks.</summary>
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

        // No money is credited here, on purpose -- see this type's own class remarks and IGmGrantMoneyService's
        // own remarks: the entire legacy handler body past the tier gate is dead/commented-out source, so
        // there is no legacy money-grant behavior to reproduce. A backdoor money-grant path must never be
        // added under the guise of "completing" this command.
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

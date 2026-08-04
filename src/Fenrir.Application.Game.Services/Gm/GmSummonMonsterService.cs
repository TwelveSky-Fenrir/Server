using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Gm;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Core.Packets.Shared;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Gm;

public sealed class GmSummonMonsterService(
    IEventLogRepository eventLog,
    ILogger<GmSummonMonsterService> logger) : IGmSummonMonsterService
{
    private const int Sort = 506;

    private const int AcceptedResult = 0;

    private const int RejectedResult = 1;

    private const byte SuccessOutcome = 1;

    public async ValueTask HandleAsync(GmSummonMonsterPayload packet, byte[] data, IZoneSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Elevated))
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Elevated-tier summon-monster command (sort {Sort}) without sufficient privilege -- disconnecting, no reply",
                zoneSession.CharacterId, Sort);
            await eventLog.LogAsync(GmActionEventCodes.SummonMonster, EventLogCategory.GmAction,
                zoneSession.AccountId, zoneSession.CharacterId, null, null, null, null, null, null, null,
                GmCommandCatalog.OutcomeDenied,
                $"Command=MONCALL;Sort={Sort};RequiredTier={(short)GmCommandTier.Elevated}", cancellationToken);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var actorResult = await zone.PostTribeProgressCommandAndWaitForResultAsync(
            new TribeProgressZoneCommand(state.CharacterId, GmSummonMonsterTemplateId: packet.Value),
            cancellationToken);
        if (actorResult.Kind != ZoneCommandResultKind.Applied)
        {
            logger.LogWarning(
                "Character {CharacterId} summon-monster command was not applied (monster {MonsterId}, map {MapId}, result {Result}, cause {Cause})",
                state.CharacterId, packet.Value, zone.MapId, actorResult.Kind, actorResult.Cause);
            await eventLog.LogAsync(GmActionEventCodes.SummonMonster, EventLogCategory.GmAction,
                zoneSession.AccountId, zoneSession.CharacterId, null, null, null, null, null, null, null,
                GmCommandCatalog.OutcomeRejected,
                $"MonsterId={packet.Value};MapId={zone.MapId};Result={actorResult.Kind};Cause={actorResult.Cause}",
                cancellationToken);
            zoneSession.Send(new GenericActionResponse
                { Result = RejectedResult, Sort = Sort, Data = data, RuneValue = 0 });
            return;
        }

        await eventLog.LogAsync(GmActionEventCodes.SummonMonster, EventLogCategory.GmAction, zoneSession.AccountId,
            zoneSession.CharacterId, null, null, null, null, null, null, null, SuccessOutcome,
            $"MonsterId={packet.Value};MapId={zone.MapId}", cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} applied the Elevated-tier summon-monster command (monster {MonsterId}, map {MapId})",
            state.CharacterId, packet.Value, zone.MapId);

        zoneSession.Send(new GenericActionResponse
            { Result = AcceptedResult, Sort = Sort, Data = data, RuneValue = 0 });
    }
}

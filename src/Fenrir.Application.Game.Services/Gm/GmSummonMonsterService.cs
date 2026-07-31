using Fenrir.Application.Game.Abstractions.Gm;
using Fenrir.Application.Game.Abstractions.Sessions;
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

    private const byte SuccessOutcome = 1;

    public async ValueTask HandleAsync(GmSummonMonsterPayload packet, byte[] data, IZoneSession zoneSession,
        PlayerRuntimeState state, Zone zone, CancellationToken cancellationToken)
    {
        if (!zoneSession.MeetsGmTier(GmCommandTier.Elevated))
        {
            logger.LogWarning(
                "Character {CharacterId} attempted the Elevated-tier summon-monster command (sort {Sort}) without sufficient privilege -- disconnecting, no reply",
                zoneSession.CharacterId, Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (!await zone.PostTribeProgressCommandAndWaitAsync(
                new TribeProgressZoneCommand(state.CharacterId, GmSummonMonsterTemplateId: packet.Value),
                cancellationToken))
            logger.LogError(
                "Zone {MapId} tribe-progress inbox full: dropped summon-monster mirror for character {CharacterId} (monster {MonsterId}) -- reporting accepted regardless, matching legacy's own unconditional success result",
                zone.MapId, state.CharacterId, packet.Value);

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

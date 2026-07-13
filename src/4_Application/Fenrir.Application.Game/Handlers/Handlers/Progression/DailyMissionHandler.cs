using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Application.Game;
using Fenrir.Core.Packets.Shared;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Progression;

public sealed class DailyMissionHandler(IDailyMissionService dailyMissionService, ILogger<DailyMissionHandler> logger)
    : IAsyncPacketHandler<DailyMissionRequest>
{
    public async ValueTask HandleAsync(DailyMissionRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        logger.LogDebug(
            "Session {SessionId}: DailyMissionRequest (op126) received for character {CharacterId}, sort {Sort}",
            session.SessionId, characterId, packet.Sort);

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (packet.Sort is not (1 or 2))
        {
            logger.LogWarning(
                "Daily-mission request rejected for character {CharacterId}: invalid sort {Sort} -- aborting session",
                characterId, packet.Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (packet.Sort == 1)
        {
            SendResult(session, packet.Sort, 0, state);
            return;
        }

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            var result = await dailyMissionService.ClaimAsync(characterId, zone, state, cancellationToken);

            switch (result.Outcome)
            {
                case DailyMissionClaimOutcome.Aborted:
                    logger.LogDebug(
                        "Daily-mission claim rejected for character {CharacterId}: requirements not met -- aborting session",
                        characterId);
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;
                case DailyMissionClaimOutcome.InventoryFull:
                    logger.LogInformation(
                        "Daily-mission claim denied for character {CharacterId}: inventory full",
                        characterId);
                    SendResult(session, packet.Sort, 3, state);
                    return;
                case DailyMissionClaimOutcome.Success:
                default:
                    SendResult(session, packet.Sort, 0, state, result.JoinWar, result.KillOtherTribe);
                    return;
            }
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private static void SendResult(IPacketSession session, int sort, int result, PlayerRuntimeState state,
        int? joinWarOverride = null, int? killOtherTribeOverride = null)
    {
        session.Send(new DailyMissionResponse
        {
            Sort = sort,
            Result = result,
            Mission = new MissionDate
            {
                JoinWar = joinWarOverride ?? state.MissionJoinWar,
                KillOtherTribe = killOtherTribeOverride ?? state.MissionKillOtherTribe,
                KillMonster = state.MissionKillMonster,
                PlayTime = state.MissionPlayTime
            }
        });
    }
}

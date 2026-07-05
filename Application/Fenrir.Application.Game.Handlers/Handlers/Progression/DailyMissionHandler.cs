using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Progression;

/// <summary>
///     CZ_MISSION_COMPLETE_SEND (opcode 126) -- daily mission view/claim. Claim gates on level &gt;= LV_M1 and
///     war/kill-tribe thresholds; <c>aKillMonster</c>/<c>aPlayTime</c> gates are compiled out in EU33 so they
///     never block a claim. A full inventory on claim is a clean failure (<c>Result = 3</c>).
/// </summary>
/// <remarks>
///     <see cref="PlayerRuntimeState.MissionJoinWar" /> still has no increment hook in Fenrir (war tracking out of
///     scope), so a claim needs a war-system gap to close before it's reachable end to end.
///     <see cref="PlayerRuntimeState.MissionKillOtherTribe" /> DOES increment now --
///     <c>Zone.ApplyPvpKillMissionProgress</c>,
///     gated by <see cref="Combat.KillCooldownTracker" /> (C05) -- so a claim is blocked only by the join-war side today.
/// </remarks>
public sealed class DailyMissionHandler(IDailyMissionService dailyMissionService)
    : IAsyncPacketHandler<DailyMissionRequest>
{
    public async ValueTask HandleAsync(DailyMissionRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null)
            return;

        if (packet.Sort is not (1 or 2))
        {
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
                    zoneSession.Abort(DisconnectReason.Faulted);
                    return;
                case DailyMissionClaimOutcome.InventoryFull:
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

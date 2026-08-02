using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Tribes;

public sealed class TribeActionHandler(
    ITribeActionService tribeActionService,
    ILogger<TribeActionHandler>? logger = null) : IAsyncPacketHandler<TribeActionRequest>
{
    public async ValueTask HandleAsync(TribeActionRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;

        logger?.LogDebug("Session {SessionId}: CZ_TRIBE_WORK_SEND received (character {CharacterId}, sort {Sort})",
            session.SessionId, zoneSession.CharacterId, packet.Sort);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await DispatchAsync(packet, session, zone, state, characterId, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask DispatchAsync(TribeActionRequest packet, IPacketSession session,
        Zone zone, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        switch (packet.Sort)
        {
            case 1:
                Respond(session, packet, characterId,
                    await tribeActionService.ResetStatsAsync(zone, state, characterId, ct));
                return;
            case 2:
                Respond(session, packet, characterId,
                    await tribeActionService.AppointSubMasterAsync(zone, state, packet.Data, ct));
                return;
            case 3:
                Respond(session, packet, characterId,
                    await tribeActionService.RemoveSubMasterAsync(zone, state, packet.Data, ct));
                return;
            case 4:
                Respond(session, packet, characterId,
                    await tribeActionService.UseTribeWeaponAsync(zone, state, characterId, ct));
                return;
            case 5:
                Respond(session, packet, characterId,
                    tribeActionService.ValidateTribeSkill(state, packet.Data));
                return;
            case 6:
                Respond(session, packet, characterId,
                    await tribeActionService.PurchaseTitleAsync(zone, state, characterId, packet.Data, ct));
                return;
            case 7:
                Respond(session, packet, characterId,
                    await tribeActionService.HaloEnchantAsync(zone, state, characterId, ct));
                return;
            case 8:
                Respond(session, packet, characterId,
                    await tribeActionService.ClaimLevelBonusAsync(zone, state, characterId, ct));
                return;
            case 9:
                Respond(session, packet, characterId,
                    await tribeActionService.SetOrnamentAsync(zone, state, characterId, true, ct));
                return;
            case 10:
                Respond(session, packet, characterId,
                    await tribeActionService.SetOrnamentAsync(zone, state, characterId, false, ct));
                return;
            case 11:
                Respond(session, packet, characterId,
                    await tribeActionService.RebirthAsync(zone, state, characterId, ct));
                return;
            case 12:
            case 13:
            case 14:
            case 15:
                logger?.LogDebug(
                    "Character {CharacterId} sent CZ_TRIBE_WORK_SEND sort {Sort} (dead sub-command) -- ignoring session {SessionId}",
                    characterId, packet.Sort, session.SessionId);
                return;
            case 16:
                Respond(session, packet, characterId,
                    await tribeActionService.RedeemMapScrollAsync(zone, state, characterId, ct));
                return;
            case 17:
                Respond(session, packet, characterId,
                    await tribeActionService.RedeemAlertCharmAsync(zone, state, characterId, ct));
                return;
            case 18:
                Respond(session, packet, characterId,
                    await tribeActionService.UseTowerScrollAsync(zone, state, characterId, ct));
                return;
            default:
                logger?.LogWarning(
                    "Character {CharacterId} sent CZ_TRIBE_WORK_SEND with unrecognized sort {Sort} -- ignoring session {SessionId}",
                    characterId, packet.Sort, session.SessionId);
                return;
        }
    }

    private void Respond(IPacketSession session, TribeActionRequest packet,
        int characterId, TribeActionOutcome outcome)
    {
        if (outcome.Aborted)
        {
            logger?.LogWarning(
                "Character {CharacterId} tribe action sort {Sort} precondition/authorization gate failed (session {SessionId})",
                characterId, packet.Sort, session.SessionId);
            return;
        }

        session.Send(new TribeActionResponse { Result = outcome.Result, Sort = packet.Sort, Data = packet.Data });
    }
}

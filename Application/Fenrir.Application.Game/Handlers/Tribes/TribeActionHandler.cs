using Fenrir.Application.Game.Handlers.Tribes.Services;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Tribes;

/// <summary>
///     CZ_TRIBE_WORK_SEND (opcode 79) -- the generic tribe sub-command channel. Sub-commands 12-15 always
///     abort; unrecognized sorts also abort. Unlike GUILD_WORK, every mutation here is either this
///     character's own progression state (write-behind) or a synchronous money debit.
/// </summary>
/// <remarks>ZC_TRIBE_WORK_RECV always echoes the client's raw tData back verbatim, never server-computed content.</remarks>
public sealed class TribeActionHandler(ITribeActionService tribeActionService) : IAsyncPacketHandler<TribeActionRequest>
{
    public async ValueTask HandleAsync(TribeActionRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        await state.EconomyActionLock.WaitAsync(cancellationToken);
        try
        {
            await DispatchAsync(packet, session, zoneSession, zone, state, characterId, cancellationToken);
        }
        finally
        {
            state.EconomyActionLock.Release();
        }
    }

    private async ValueTask DispatchAsync(TribeActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        switch (packet.Sort)
        {
            case 1:
                Respond(session, zoneSession, packet,
                    await tribeActionService.ResetStatsAsync(zone, state, characterId, ct));
                return;
            case 2:
                Respond(session, zoneSession, packet,
                    await tribeActionService.AppointSubMasterAsync(zone, state, packet.Data, ct));
                return;
            case 3:
                Respond(session, zoneSession, packet,
                    await tribeActionService.RemoveSubMasterAsync(zone, state, packet.Data, ct));
                return;
            case 4:
                Respond(session, zoneSession, packet,
                    await tribeActionService.UseTribeWeaponAsync(zone, state, characterId, ct));
                return;
            case 5:
                Respond(session, zoneSession, packet, tribeActionService.ValidateTribeSkill(state, packet.Data));
                return;
            case 6:
                Respond(session, zoneSession, packet,
                    await tribeActionService.PurchaseTitleAsync(zone, state, characterId, packet.Data, ct));
                return;
            case 7:
                Respond(session, zoneSession, packet,
                    await tribeActionService.HaloEnchantAsync(zone, state, characterId, ct));
                return;
            case 8:
                Respond(session, zoneSession, packet,
                    await tribeActionService.ClaimLevelBonusAsync(zone, state, characterId, ct));
                return;
            case 9:
                Respond(session, zoneSession, packet,
                    await tribeActionService.SetOrnamentAsync(zone, state, characterId, true, ct));
                return;
            case 10:
                Respond(session, zoneSession, packet,
                    await tribeActionService.SetOrnamentAsync(zone, state, characterId, false, ct));
                return;
            case 11:
                Respond(session, zoneSession, packet,
                    await tribeActionService.RebirthAsync(zone, state, characterId, ct));
                return;
            case 12:
            case 13:
            case 14:
            case 15:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case 16:
                Respond(session, zoneSession, packet,
                    await tribeActionService.RedeemMapScrollAsync(zone, state, characterId, ct));
                return;
            case 17:
                Respond(session, zoneSession, packet,
                    await tribeActionService.RedeemAlertCharmAsync(zone, state, characterId, ct));
                return;
            case 18:
                Respond(session, zoneSession, packet,
                    await tribeActionService.UseTowerScrollAsync(zone, state, characterId, ct));
                return;
            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }
    }

    private static void Respond(IPacketSession session, ZoneClientSession zoneSession, TribeActionRequest packet,
        TribeActionOutcome outcome)
    {
        if (outcome.Aborted)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        session.Send(new TribeActionResponse { Result = outcome.Result, Sort = packet.Sort, Data = packet.Data });
    }
}

using Fenrir.Application.Game.Handlers.Guilds.Services;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Guilds;

/// <summary>
///     CZ_GUILD_WORK_SEND (opcode 75) -- the generic guild sub-command channel. Dead sub-commands reproduce
///     their exact legacy shape: 11 always aborts; 12/13 are a silent no-op (the legacy's own abort call is
///     commented out); anything else falls to the default abort.
/// </summary>
/// <remarks>
///     Membership changes (join/leave/kick/promote/transfer) still only mirror onto the specific character(s)
///     whose own <c>PlayerRuntimeState</c> changed, via <c>GuildMembershipZoneCommand</c> -- a guild-wide
///     GUILD_INFO push for those would still leave every other member's roster view stale on its own next query,
///     but membership rows are looked up fresh every time regardless. Notice/AGM/title/buff, by contrast, mutate
///     something every member's already-cached GUILD_INFO should reflect immediately, so those four additionally
///     broadcast the refreshed GUILD_INFO to every currently connected member (see
///     <see cref="Services.IGuildActionService" />'s implementation), not just the actor (who already gets it
///     through this handler's own response send).
/// </remarks>
public sealed class GuildActionHandler(IGuildActionService service) : IAsyncPacketHandler<GuildActionRequest>
{
    public async ValueTask HandleAsync(GuildActionRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        // Every tSort here shares the same per-character economy-adjacent state (guild membership, money).
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

    private async ValueTask DispatchAsync(GuildActionRequest packet, IPacketSession session,
        ZoneClientSession zoneSession, Zone zone, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        switch (packet.Sort)
        {
            case 1:
                Respond(session, zoneSession, await service.CreateGuildAsync(packet, zone, state, characterId, ct));
                return;
            case 2:
                Respond(session, zoneSession, await service.GetGuildInfoAsync(state, ct));
                return;
            case 3:
                Respond(session, zoneSession, await service.FinalizeInviteAsync(state, characterId, ct));
                return;
            case 4:
                Respond(session, zoneSession, await service.ExitGuildAsync(zone, state, characterId, ct));
                return;
            case 5:
                Respond(session, zoneSession, await service.UpdateGuildNoticeAsync(packet, state, ct));
                return;
            case 6:
                Respond(session, zoneSession, await service.DisbandGuildAsync(zone, state, characterId, ct));
                return;
            case 7:
                Respond(session, zoneSession, await service.UpgradeGuildAsync(state, characterId, ct));
                return;
            case 8:
                Respond(session, zoneSession, await service.KickMemberAsync(packet, state, ct));
                return;
            case 9:
                Respond(session, zoneSession, await service.SetAgmRoleAsync(packet, state, ct));
                return;
            case 10:
                Respond(session, zoneSession, await service.SetMemberTitleAsync(packet, state, ct));
                return;
            case 11:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case 12:
            case 13:
                return;
            case 14:
                Respond(session, zoneSession, await service.SetGuildBuffAsync(packet, state, ct));
                return;
            case 17:
                Respond(session, zoneSession,
                    await service.TransferLeadershipAsync(packet, zone, state, characterId, ct));
                return;
            case 1001:
                Respond(session, zoneSession, await service.SetGuildLogoAsync(packet, state, ct));
                return;
            default:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }
    }

    private static void Respond(IPacketSession session, ZoneClientSession zoneSession, GuildActionResult result)
    {
        if (result.Abort)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        session.Send(new GuildActionResponse { Result = result.Result, Sort = result.Sort, GuildInfo = result.GuildInfo });
    }
}

using Fenrir.Application.Game.Abstractions.Guilds;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Guilds;

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
///     <see cref="IGuildActionService" />'s implementation), not just the actor (who already gets it
///     through this handler's own response send).
/// </remarks>
public sealed class GuildActionHandler(IGuildActionService service, ILogger<GuildActionHandler>? logger = null)
    : IAsyncPacketHandler<GuildActionRequest>
{
    public async ValueTask HandleAsync(GuildActionRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;

        logger?.LogDebug("Session {SessionId}: CZ_GUILD_WORK_SEND received (character {CharacterId}, sort {Sort})",
            session.SessionId, zoneSession.CharacterId, packet.Sort);

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
                Respond(session, zoneSession, characterId, packet.Sort,
                    await service.CreateGuildAsync(packet, zone, state, characterId, ct));
                return;
            case 2:
                Respond(session, zoneSession, characterId, packet.Sort, await service.GetGuildInfoAsync(state, ct));
                return;
            case 3:
                Respond(session, zoneSession, characterId, packet.Sort,
                    await service.FinalizeInviteAsync(state, characterId, ct));
                return;
            case 4:
                Respond(session, zoneSession, characterId, packet.Sort,
                    await service.ExitGuildAsync(zone, state, characterId, ct));
                return;
            case 5:
                Respond(session, zoneSession, characterId, packet.Sort,
                    await service.UpdateGuildNoticeAsync(packet, state, ct));
                return;
            case 6:
                Respond(session, zoneSession, characterId, packet.Sort,
                    await service.DisbandGuildAsync(zone, state, characterId, ct));
                return;
            case 7:
                Respond(session, zoneSession, characterId, packet.Sort,
                    await service.UpgradeGuildAsync(state, characterId, ct));
                return;
            case 8:
                Respond(session, zoneSession, characterId, packet.Sort,
                    await service.KickMemberAsync(packet, state, ct));
                return;
            case 9:
                Respond(session, zoneSession, characterId, packet.Sort,
                    await service.SetAgmRoleAsync(packet, state, ct));
                return;
            case 10:
                Respond(session, zoneSession, characterId, packet.Sort,
                    await service.SetMemberTitleAsync(packet, state, ct));
                return;
            case 11:
                // Dead sub-command: the legacy's own abort call for this sort is unconditional (see class
                // remarks) -- Debug, not Warning, since an unmodified legacy client can send this too, it is
                // not itself a sign of a misbehaving/modified client.
                logger?.LogDebug(
                    "Character {CharacterId} sent CZ_GUILD_WORK_SEND sort 11 (dead sub-command) -- aborting session {SessionId}",
                    characterId, session.SessionId);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            case 12:
            case 13:
                return;
            case 14:
                Respond(session, zoneSession, characterId, packet.Sort,
                    await service.SetGuildBuffAsync(packet, state, ct));
                return;
            case 17:
                Respond(session, zoneSession, characterId, packet.Sort,
                    await service.TransferLeadershipAsync(packet, zone, state, characterId, ct));
                return;
            case 1001:
                Respond(session, zoneSession, characterId, packet.Sort,
                    await service.SetGuildLogoAsync(packet, state, ct));
                return;
            default:
                // Unrecognized tSort -- an unmodified legacy client never sends one, so this is worth a
                // higher-visibility trace than the known-dead sort 11 case above.
                logger?.LogWarning(
                    "Character {CharacterId} sent CZ_GUILD_WORK_SEND with unrecognized sort {Sort} -- aborting session {SessionId}",
                    characterId, packet.Sort, session.SessionId);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
        }
    }

    private void Respond(IPacketSession session, ZoneClientSession zoneSession, int characterId, int sort,
        GuildActionResult result)
    {
        if (result.Abort)
        {
            logger?.LogWarning(
                "Character {CharacterId} guild action sort {Sort} aborted session {SessionId}: precondition/authorization gate failed",
                characterId, sort, session.SessionId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        session.Send(new GuildActionResponse
            { Result = result.Result, Sort = result.Sort, GuildInfo = result.GuildInfo });
    }
}

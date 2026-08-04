using Fenrir.Application.Game.Abstractions.Guilds;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Guilds;

public sealed class GuildActionHandler(IGuildActionService service, ILogger<GuildActionHandler>? logger = null)
    : IAsyncPacketHandler<GuildActionRequest>
{
    public async ValueTask HandleAsync(GuildActionRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;

        logger?.LogDebug("Session {SessionId}: CZ_GUILD_WORK_SEND received (character {CharacterId}, sort {Sort})",
            session.SessionId, zoneSession.CharacterId, packet.Sort);

        if (packet.Sort is 11 or
            not (1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 12 or 13 or 14 or 17 or 1001))
        {
            logger?.LogWarning(
                "Session {SessionId}: CZ_GUILD_WORK_SEND carried invalid sort {Sort}; aborting",
                session.SessionId, packet.Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

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

    private async ValueTask DispatchAsync(GuildActionRequest packet, IPacketSession session,
        Zone zone, PlayerRuntimeState state, int characterId, CancellationToken ct)
    {
        switch (packet.Sort)
        {
            case 1:
                Respond(session, characterId, packet.Sort,
                    await service.CreateGuildAsync(packet, zone, state, characterId, ct));
                return;
            case 2:
                if (state.GuildId is null)
                {
                    logger?.LogWarning(
                        "Character {CharacterId} requested guild info without a guild; aborting session {SessionId}",
                        characterId, session.SessionId);
                    session.Abort(DisconnectReason.Faulted);
                    return;
                }

                Respond(session, characterId, packet.Sort, await service.GetGuildInfoAsync(state, ct));
                return;
            case 3:
                Respond(session, characterId, packet.Sort,
                    await service.FinalizeInviteAsync(state, characterId, ct));
                return;
            case 4:
                Respond(session, characterId, packet.Sort,
                    await service.ExitGuildAsync(zone, state, characterId, ct));
                return;
            case 5:
                Respond(session, characterId, packet.Sort,
                    await service.UpdateGuildNoticeAsync(packet, state, ct));
                return;
            case 6:
                Respond(session, characterId, packet.Sort,
                    await service.DisbandGuildAsync(zone, state, characterId, ct));
                return;
            case 7:
                Respond(session, characterId, packet.Sort,
                    await service.UpgradeGuildAsync(state, characterId, ct));
                return;
            case 8:
                Respond(session, characterId, packet.Sort,
                    await service.KickMemberAsync(packet, state, ct));
                return;
            case 9:
                Respond(session, characterId, packet.Sort,
                    await service.SetAgmRoleAsync(packet, state, ct));
                return;
            case 10:
                Respond(session, characterId, packet.Sort,
                    await service.SetMemberTitleAsync(packet, state, ct));
                return;
            case 12:
            case 13:
                return;
            case 14:
                Respond(session, characterId, packet.Sort,
                    await service.SetGuildBuffAsync(packet, state, ct));
                return;
            case 17:
                Respond(session, characterId, packet.Sort,
                    await service.TransferLeadershipAsync(packet, zone, state, characterId, ct));
                return;
            case 1001:
                Respond(session, characterId, packet.Sort,
                    await service.SetGuildLogoAsync(packet, state, ct));
                return;
            default:
                logger?.LogWarning(
                    "Character {CharacterId} sent CZ_GUILD_WORK_SEND with invalid sort {Sort}; aborting session {SessionId}",
                    characterId, packet.Sort, session.SessionId);
                session.Abort(DisconnectReason.Faulted);
                return;
        }
    }

    private void Respond(IPacketSession session, int characterId, int sort, GuildActionResult result)
    {
        if (result.Abort)
        {
            logger?.LogWarning(
                "Character {CharacterId} guild action sort {Sort} precondition/authorization gate failed (session {SessionId})",
                characterId, sort, session.SessionId);
            return;
        }

        session.Send(new GuildActionResponse
            { Result = result.Result, Sort = result.Sort, GuildInfo = result.GuildInfo });
    }
}

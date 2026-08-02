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
            case 11:
                logger?.LogInformation(
                    "Character {CharacterId} sent CZ_GUILD_WORK_SEND sort 11 (GuildMark) -- unconditional disconnect, no response sent (Server/ts25zone/S04_MyWork02.cpp:10230-10232), session {SessionId}",
                    characterId, session.SessionId);
                session.Abort(DisconnectReason.Faulted);
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
                    "Character {CharacterId} sent CZ_GUILD_WORK_SEND with unrecognized sort {Sort} -- ignoring session {SessionId}",
                    characterId, packet.Sort, session.SessionId);
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

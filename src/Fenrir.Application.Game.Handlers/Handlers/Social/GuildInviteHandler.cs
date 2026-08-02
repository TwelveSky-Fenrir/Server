using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class GuildInviteHandler(
    IGuildInviteService guildInviteService,
    ILogger<GuildInviteHandler>? logger = null) : IAsyncPacketHandler<GuildInviteRequest>
{
    public async ValueTask HandleAsync(GuildInviteRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (IZoneSession)session;

        logger?.LogDebug(
            "Session {SessionId}: CZ_GUILD_ASK_SEND received (character {CharacterId}, target {AvatarName})",
            session.SessionId, zoneSession.CharacterId, packet.AvatarName);

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var askerId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(askerId, out var asker) || asker is null)
            return;

        var result = await guildInviteService.AskAsync(asker, packet.AvatarName, cancellationToken)
            .ConfigureAwait(false);

        switch (result)
        {
            case GuildInviteAskResultKind.NotAuthorized:
            case GuildInviteAskResultKind.TargetAlreadyGuilded:
            case GuildInviteAskResultKind.TribeMismatch:
                return;
            case GuildInviteAskResultKind.TargetNotFound:
                session.Send(new GuildInviteAnswerResponse { Answer = 4 });
                return;
            case GuildInviteAskResultKind.AskerBusy:
                session.Send(new GuildInviteAnswerResponse { Answer = 3 });
                return;
            case GuildInviteAskResultKind.TargetBusy:
                session.Send(new GuildInviteAnswerResponse { Answer = 5 });
                return;
            case GuildInviteAskResultKind.SentCrossShard:
                return;
        }
    }
}

using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class GuildInviteCancelHandler(
    IGuildInviteService guildInviteService,
    ILogger<GuildInviteCancelHandler>? logger = null) : IInlinePacketHandler<GuildInviteCancelRequest>
{
    public void Handle(in GuildInviteCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var askerId = zoneSession.CharacterId!.Value;

        logger?.LogDebug("Session {SessionId}: CZ_GUILD_CANCEL_SEND received (character {CharacterId})",
            session.SessionId, askerId);

        guildInviteService.Cancel(askerId);
    }
}

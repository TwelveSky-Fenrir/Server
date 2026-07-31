using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Application.Game.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

public sealed class GuildInviteAnswerHandler(
    IGuildInviteService guildInviteService,
    ILogger<GuildInviteAnswerHandler>? logger = null) : IInlinePacketHandler<GuildInviteAnswerRequest>
{
    public void Handle(in GuildInviteAnswerRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        logger?.LogDebug(
            "Session {SessionId}: CZ_GUILD_ANSWER_SEND received (character {CharacterId}, answer {Answer})",
            session.SessionId, zoneSession.CharacterId, packet.Answer);

        if (packet.Answer is not (0 or 1 or 2))
            return;

        var targetId = zoneSession.CharacterId!.Value;

        guildInviteService.Answer(targetId, packet.Answer);
    }
}

using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>
///     CZ_GUILD_ANSWER_SEND (opcode 74) -- accept only reaches negotiation state 3; asker must still send
///     CZ_GUILD_WORK_SEND tSort 3 to finalize.
/// </summary>
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

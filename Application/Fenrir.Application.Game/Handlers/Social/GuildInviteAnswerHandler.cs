using Fenrir.Application.Game.Guilds;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_GUILD_ANSWER_SEND (opcode 74) -- accept only reaches negotiation state 3; asker must still send
///     CZ_GUILD_WORK_SEND tSort 3 to finalize.
/// </summary>
public sealed class GuildInviteAnswerHandler(IGuildInviteService guildInviteService)
    : IInlinePacketHandler<GuildInviteAnswerRequest>
{
    public void Handle(in GuildInviteAnswerRequest packet, IPacketSession session)
    {
        if (packet.Answer is not (0 or 1 or 2))
            return;

        var zoneSession = (ZoneClientSession)session;
        var targetId = zoneSession.CharacterId!.Value;

        guildInviteService.Answer(targetId, packet.Answer);
    }
}

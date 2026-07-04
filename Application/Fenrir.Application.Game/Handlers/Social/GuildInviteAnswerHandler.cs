using Fenrir.Application.Game.Guilds;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>
///     CZ_GUILD_ANSWER_SEND (opcode 74) -- accept only reaches negotiation state 3; asker must still send
///     CZ_GUILD_WORK_SEND tSort 3 to finalize.
/// </summary>
public sealed class GuildInviteAnswerHandler(ZoneRegistry zones, GuildInviteRegistry invites)
    : IInlinePacketHandler<GuildInviteAnswerRequest>
{
    public void Handle(in GuildInviteAnswerRequest packet, IPacketSession session)
    {
        if (packet.Answer is not (0 or 1 or 2))
            return;

        var zoneSession = (ZoneClientSession)session;
        var targetId = zoneSession.CharacterId!.Value;

        if (!invites.TryAnswer(targetId, packet.Answer == 0, out var askerId))
            return;

        if (zones.TryGetPlayer(askerId, out var asker))
            asker.Session.Send(new GuildInviteAnswerResponse { Answer = packet.Answer });
    }
}

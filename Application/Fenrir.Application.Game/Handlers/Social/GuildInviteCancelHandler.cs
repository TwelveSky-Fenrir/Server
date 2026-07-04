using Fenrir.Application.Game.Guilds;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>CZ_GUILD_CANCEL_SEND (opcode 73) -- withdraws the caller's own still-pending guild invitation ask.</summary>
public sealed class GuildInviteCancelHandler(ZoneRegistry zones, GuildInviteRegistry invites)
    : IInlinePacketHandler<GuildInviteCancelRequest>
{
    public void Handle(in GuildInviteCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var askerId = zoneSession.CharacterId!.Value;

        if (!invites.TryCancel(askerId, out var targetId))
            return;

        if (zones.TryGetPlayer(targetId, out var target))
            target.Session.Send(new GuildInviteCancelResponse());
    }
}

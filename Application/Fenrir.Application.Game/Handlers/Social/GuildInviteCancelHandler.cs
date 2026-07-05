using Fenrir.Application.Game.Handlers.Social.Services;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>CZ_GUILD_CANCEL_SEND (opcode 73) -- withdraws the caller's own still-pending guild invitation ask.</summary>
public sealed class GuildInviteCancelHandler(IGuildInviteService guildInviteService)
    : IInlinePacketHandler<GuildInviteCancelRequest>
{
    public void Handle(in GuildInviteCancelRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        var askerId = zoneSession.CharacterId!.Value;

        guildInviteService.Cancel(askerId);
    }
}

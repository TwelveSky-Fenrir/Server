using Fenrir.Application.Game.Abstractions.Social;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

/// <summary>CZ_GUILD_CANCEL_SEND (opcode 73) -- withdraws the caller's own still-pending guild invitation ask.</summary>
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

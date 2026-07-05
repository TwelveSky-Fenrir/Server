using Fenrir.Application.Game.Handlers.Chat.Services;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Chat;

/// <summary>
///     CZ_GUILD_NOTICE_SEND (opcode 76). Restricted to the guild master (<c>GuildRoleCodec.IsMaster</c>);
///     a non-master sender is silently ignored, not disconnected. No mute gate applies to this channel.
/// </summary>
public sealed class GuildAnnouncementHandler(IGuildAnnouncementService guildAnnouncementService)
    : IInlinePacketHandler<GuildAnnouncementRequest>
{
    public void Handle(in GuildAnnouncementRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (string.IsNullOrEmpty(packet.Content))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var sender) || sender is null)
            return;

        guildAnnouncementService.TrySendAnnouncement(sender, packet.Content);
    }
}

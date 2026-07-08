using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

/// <summary>
///     CZ_GUILD_NOTICE_SEND (opcode 76). Restricted to the guild master (<c>GuildRoleCodec.IsMaster</c>);
///     a non-master sender is silently ignored, not disconnected. No mute gate applies to this channel.
/// </summary>
public sealed class GuildAnnouncementHandler(
    IGuildAnnouncementService guildAnnouncementService,
    ILogger<GuildAnnouncementHandler>? logger = null) : IInlinePacketHandler<GuildAnnouncementRequest>
{
    public void Handle(in GuildAnnouncementRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        logger?.LogDebug(
            "Session {SessionId}: CZ_GUILD_NOTICE_SEND received (character {CharacterId}, content length {ContentLength})",
            session.SessionId, zoneSession.CharacterId, packet.Content.Length);

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

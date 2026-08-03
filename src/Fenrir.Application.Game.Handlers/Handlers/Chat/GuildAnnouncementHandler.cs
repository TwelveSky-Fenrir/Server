using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

public sealed class GuildAnnouncementHandler(
    IGuildAnnouncementService guildAnnouncementService,
    ILogger<GuildAnnouncementHandler>? logger = null) : IInlinePacketHandler<GuildAnnouncementRequest>
{
    public void Handle(in GuildAnnouncementRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;

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

using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Tribes;

public sealed class TribeAnnouncementScrollHandler(
    ITribeAnnouncementScrollService announcementService,
    ILogger<TribeAnnouncementScrollHandler>? logger = null) : IInlinePacketHandler<TribeAnnouncementScrollRequest>
{
    public void Handle(in TribeAnnouncementScrollRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;

        logger?.LogDebug(
            "Session {SessionId}: CZ_TRIBE_NOTIFY_SEND received (character {CharacterId}, content length {ContentLength})",
            session.SessionId, zoneSession.CharacterId, packet.Content.Length);

        var content = ChatRouter.SafeContent(packet.Content);

        if (ChatRouter.IsContentEmpty(content))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var sender) || sender is null)
            return;

        if (sender.IsMuted)
            return;

        if (!announcementService.TryBroadcast(zone, sender, characterId, session, content))
        {
            logger?.LogWarning(
                "Character {CharacterId} tribe-notify scroll precondition failed; disconnecting with no response sent (session {SessionId})",
                characterId, session.SessionId);
            session.Abort(DisconnectReason.Faulted);
        }
    }
}

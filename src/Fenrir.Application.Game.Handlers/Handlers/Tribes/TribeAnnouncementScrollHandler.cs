using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers.Tribes;

public sealed class TribeAnnouncementScrollHandler(
    ITribeAnnouncementScrollService announcementService,
    ILogger<TribeAnnouncementScrollHandler>? logger = null) : IInlinePacketHandler<TribeAnnouncementScrollRequest>
{
    public void Handle(in TribeAnnouncementScrollRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        logger?.LogDebug(
            "Session {SessionId}: CZ_TRIBE_NOTIFY_SEND received (character {CharacterId}, content length {ContentLength})",
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

        if (!announcementService.TryBroadcast(zone, sender, characterId, session, packet.Content))
            zoneSession.Abort(DisconnectReason.Faulted);
    }
}

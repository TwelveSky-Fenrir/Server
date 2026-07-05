using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

/// <summary>
///     CZ_TRIBE_NOTICE_SEND (opcode 80). Restricted to tribe master/sub-master
///     (<see cref="PlayerRuntimeState.TribeRole" /> 1 or 2); a regular member is silently ignored, not
///     disconnected. Strict tribe match only, no alliance.
/// </summary>
public sealed class TribeAnnouncementHandler(ITribeAnnouncementService tribeAnnouncementService)
    : IInlinePacketHandler<TribeAnnouncementRequest>
{
    public void Handle(in TribeAnnouncementRequest packet, IPacketSession session)
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

        tribeAnnouncementService.TrySendAnnouncement(sender, packet.Content);
    }
}

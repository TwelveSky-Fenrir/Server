using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Chat;

/// <summary>
///     CZ_TRIBE_NOTICE_SEND (opcode 80). Restricted to tribe master/sub-master
///     (<see cref="PlayerRuntimeState.TribeRole" /> 1 or 2); a regular member is silently ignored, not
///     disconnected. Strict tribe match only, no alliance.
/// </summary>
public sealed class TribeAnnouncementHandler(ZoneRegistry zones) : IInlinePacketHandler<TribeAnnouncementRequest>
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

        if (sender.TribeRole is not (1 or 2))
            return;

        var response = new TribeAnnouncementResponse
            { TribeRole = sender.TribeRole, AvatarName = sender.Name, Content = packet.Content };

        foreach (var target in zones.Zones)
        foreach (var recipient in target.Players)
            if (recipient.Tribe == sender.Tribe)
                recipient.Session.Send(response);
    }
}

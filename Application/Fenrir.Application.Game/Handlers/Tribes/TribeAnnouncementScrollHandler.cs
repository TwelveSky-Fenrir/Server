using Fenrir.Application.Game.Tribes;
using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers.Tribes;

/// <summary>
///     CZ_TRIBE_NOTIFY_SEND (opcode 112). Consumes one <see cref="PlayerRuntimeState.TribeNotifyScrollCount" />
///     charge and relays same-tribe, across every zone of this process (this build's <c>LNW33</c> branch;
///     the "send to everyone" branch is dead code here) -- unlike <see cref="TribeAnnouncementHandler" />
///     (op 80), there is no role gate at all. <see cref="TribeAnnouncementScrollResponse.TribeRole" /> on the
///     wire actually carries the sender's tribe number, not a role (see that contract's own docstring).
/// </summary>
public sealed class TribeAnnouncementScrollHandler(ZoneRegistry zones)
    : IInlinePacketHandler<TribeAnnouncementScrollRequest>
{
    public void Handle(in TribeAnnouncementScrollRequest packet, IPacketSession session)
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

        if (sender.TribeNotifyScrollCount < 1)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        var newCount = sender.TribeNotifyScrollCount - 1;

        session.Send(new AvatarStatUpdateResponse { Sort = 69, Value = newCount, Value2 = 0 });

        var response = new TribeAnnouncementScrollResponse
            { TribeRole = sender.Tribe, AvatarName = sender.Name, Content = packet.Content };

        foreach (var target in zones.Zones)
        foreach (var recipient in target.Players)
            if (recipient.Tribe == sender.Tribe)
                recipient.Session.Send(response);

        zone.PostTribeProgressCommand(new TribeProgressZoneCommand(characterId, TribeNotifyScrollCount: newCount));
    }
}

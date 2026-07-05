using Fenrir.Application.Game.Abstractions.Tribes;
using Fenrir.Application.Game.Domain.Tribes;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Services.Tribes;

/// <summary>See <see cref="ITribeAnnouncementScrollService" />.</summary>
public sealed class TribeAnnouncementScrollService(ZoneRegistry zones) : ITribeAnnouncementScrollService
{
    public bool TryBroadcast(Zone zone, PlayerRuntimeState sender, int characterId, IPacketSession session,
        string content)
    {
        if (sender.TribeNotifyScrollCount < 1)
            return false;

        var newCount = sender.TribeNotifyScrollCount - 1;

        session.Send(new AvatarStatUpdateResponse { Sort = 69, Value = newCount, Value2 = 0 });

        var response = new TribeAnnouncementScrollResponse
            { TribeRole = sender.Tribe, AvatarName = sender.Name, Content = content };

        foreach (var target in zones.Zones)
        foreach (var recipient in target.Players)
            if (recipient.Tribe == sender.Tribe)
                recipient.Session.Send(response);

        zone.PostTribeProgressCommand(new TribeProgressZoneCommand(characterId, TribeNotifyScrollCount: newCount));

        return true;
    }
}

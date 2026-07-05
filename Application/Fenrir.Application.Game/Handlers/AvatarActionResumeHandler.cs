using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>CZ_UPDATE_AVATAR_ACTION (op16) -- same payload/handling as <see cref="AvatarActionHandler" /> (op15).</summary>
public sealed class AvatarActionResumeHandler : IInlinePacketHandler<AvatarActionResumeRequest>
{
    public void Handle(in AvatarActionResumeRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var action = packet.Action;
        zone.Post(ZoneCommand.Move(zoneSession.CharacterId!.Value, in action));
    }
}

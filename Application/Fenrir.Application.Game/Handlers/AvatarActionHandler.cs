using Fenrir.Application.Game.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Application.Game.ZoneLifecycle.Services;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>CZ_AVATAR_ACTION_SEND (op15).</summary>
public sealed class AvatarActionHandler(IAvatarActionService service) : IInlinePacketHandler<AvatarActionRequest>
{
    public void Handle(in AvatarActionRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        // Benign staleness window around a zone handoff.
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var action = packet.Action;
        service.PostAction(zone, zoneSession.CharacterId!.Value, in action);
    }
}

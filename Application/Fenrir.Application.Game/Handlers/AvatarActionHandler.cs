using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>CZ_AVATAR_ACTION_SEND (op15).</summary>
public sealed class AvatarActionHandler : IInlinePacketHandler<AvatarActionRequest>
{
    public void Handle(in AvatarActionRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        // Benign staleness window around a zone handoff.
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var action = packet.Action;
        zone.Post(ZoneCommand.Move(zoneSession.CharacterId!.Value, in action));
    }
}

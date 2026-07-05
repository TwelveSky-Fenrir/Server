using Fenrir.Application.Game.World;
using Fenrir.Application.Game.ZoneLifecycle.Services;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>CZ_UPDATE_AVATAR_ACTION (op16) -- same payload/handling as <see cref="AvatarActionHandler" /> (op15).</summary>
public sealed class AvatarActionResumeHandler(IAvatarActionService service)
    : IInlinePacketHandler<AvatarActionResumeRequest>
{
    public void Handle(in AvatarActionResumeRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var action = packet.Action;
        service.PostAction(zone, zoneSession.CharacterId!.Value, in action);
    }
}

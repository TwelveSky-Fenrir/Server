using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Handlers.Logging;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class AvatarActionResumeHandler(
    IAvatarActionService service,
    ILogger<AvatarActionResumeHandler>? logger = null) : IInlinePacketHandler<AvatarActionResumeRequest>
{
    public void Handle(in AvatarActionResumeRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var action = packet.Action;
        var characterId = zoneSession.CharacterId!.Value;

        logger?.AvatarActionReceived(session.SessionId, characterId, 16, action.Type, action.Sort);

        service.PostAction(zone, characterId, in action, true);
    }
}

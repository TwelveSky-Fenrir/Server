using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Handlers.Logging;
using Fenrir.Network.Abstractions;
using Fenrir.Application.Game;
using Fenrir.Application.Game.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

public sealed class AvatarActionHandler(IAvatarActionService service, ILogger<AvatarActionHandler>? logger = null)
    : IInlinePacketHandler<AvatarActionRequest>
{
    public void Handle(in AvatarActionRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var action = packet.Action;
        var characterId = zoneSession.CharacterId!.Value;

        logger?.AvatarActionReceived(session.SessionId, characterId, 15, action.Type, action.Sort);

        service.PostAction(zone, characterId, in action);
    }
}

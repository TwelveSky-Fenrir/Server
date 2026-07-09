using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Handlers.Logging;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Handlers.Handlers;

/// <summary>CZ_AVATAR_ACTION_SEND (op15).</summary>
public sealed class AvatarActionHandler(IAvatarActionService service, ILogger<AvatarActionHandler>? logger = null)
    : IInlinePacketHandler<AvatarActionRequest>
{
    public void Handle(in AvatarActionRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        // Benign staleness window around a zone handoff.
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var action = packet.Action;
        var characterId = zoneSession.CharacterId!.Value;

        logger?.AvatarActionReceived(session.SessionId, characterId, 15, action.Type, action.Sort);

        service.PostAction(zone, characterId, in action);
    }
}

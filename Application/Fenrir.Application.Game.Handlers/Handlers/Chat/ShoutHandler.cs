using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

/// <summary>
///     CZ_GENERAL_SHOUT_SEND (opcode 40). Silently ignored outside shout-enabled maps
///     (<see cref="ChatRouter.IsShoutEnabledOnMap" />) -- matches the legacy's silent ignore, not a Quit.
/// </summary>
public sealed class ShoutHandler(IShoutService shoutService) : IInlinePacketHandler<ShoutRequest>
{
    public void Handle(in ShoutRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;

        if (ChatRouter.IsContentEmpty(packet.Content))
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        shoutService.TryPostShout(zone, state, packet.Content, packet.Link);
    }
}

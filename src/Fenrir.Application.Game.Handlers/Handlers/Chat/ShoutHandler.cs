using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Application.Game.Domain.Social.Chat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

public sealed class ShoutHandler(IShoutService shoutService) : IInlinePacketHandler<ShoutRequest>
{
    public void Handle(in ShoutRequest packet, IPacketSession session)
    {
        var zoneSession = (IZoneSession)session;

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

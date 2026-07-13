using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Network.Abstractions;
using Fenrir.Application.Game;
using Fenrir.Application.Game.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

public sealed class GlobalAnnouncementHandler(IGlobalAnnouncementService globalAnnouncementService)
    : IInlinePacketHandler<GlobalAnnouncementRequest>
{
    public void Handle(in GlobalAnnouncementRequest packet, IPacketSession session)
    {
        globalAnnouncementService.TryAnnounce((ZoneClientSession)session, packet.Content);
    }
}

using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

public sealed class GlobalAnnouncementHandler(IGlobalAnnouncementService globalAnnouncementService)
    : IInlinePacketHandler<GlobalAnnouncementRequest>
{
    public void Handle(in GlobalAnnouncementRequest packet, IPacketSession session)
    {
        globalAnnouncementService.TryAnnounce((ZoneClientSession)session, packet.Content);
    }
}

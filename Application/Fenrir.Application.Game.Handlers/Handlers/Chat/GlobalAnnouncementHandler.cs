using Fenrir.Application.Game.Abstractions.Chat;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

/// <summary>
///     CZ_GENERAL_NOTICE_SEND (opcode 17) -- GM cluster-wide announcement. Business logic (the
///     <c>uUserSort&gt;=1</c> gate and the unfiltered fan-out) lives in <see cref="IGlobalAnnouncementService" />.
/// </summary>
public sealed class GlobalAnnouncementHandler(IGlobalAnnouncementService globalAnnouncementService)
    : IInlinePacketHandler<GlobalAnnouncementRequest>
{
    public void Handle(in GlobalAnnouncementRequest packet, IPacketSession session)
    {
        globalAnnouncementService.TryAnnounce((ZoneClientSession)session, packet.Content);
    }
}

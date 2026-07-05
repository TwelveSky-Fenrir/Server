using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Chat;

/// <summary>
///     CZ_GENERAL_NOTICE_SEND (opcode 17) -- GM global announcement. Permanently inert: Fenrir has no
///     GM-rank concept, so every sender fails the legacy's <c>uUserSort &lt; 1</c> gate and is ignored.
/// </summary>
public sealed class GlobalAnnouncementHandler : IInlinePacketHandler<GlobalAnnouncementRequest>
{
    public void Handle(in GlobalAnnouncementRequest packet, IPacketSession session)
    {
    }
}

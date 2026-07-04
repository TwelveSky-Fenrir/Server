using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>Final client ACK for zone entry (op13). Marks the session InWorld.</summary>
public sealed class ZoneReadyHandler : IInlinePacketHandler<ZoneReadyRequest>
{
    public void Handle(in ZoneReadyRequest packet, IPacketSession session)
    {
        ((ZoneClientSession)session).MarkInWorld();
    }
}

using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Application.Game.Handlers;

/// <summary>No-op: registers the heartbeat opcode so it doesn't fall through to "no handler".</summary>
public sealed class HeartbeatHandler : IInlinePacketHandler<HeartbeatRequest>
{
    public void Handle(in HeartbeatRequest packet, IPacketSession session)
    {
    }
}

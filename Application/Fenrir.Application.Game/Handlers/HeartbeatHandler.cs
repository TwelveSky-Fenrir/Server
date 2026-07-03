using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     No-op by design for M1: the legacy CRC check on tData is compiled out under DEV_SERVER (wire contract
///     §5.8, "#ifndef DEV_SERVER"), and no timeout-disconnect watchdog exists yet for a session that stops
///     heartbeating (an accepted M1 gap -- the session-level rate limiter and TCP itself are the only current
///     backstops). This handler exists purely so the opcode is registered and accepted rather than falling
///     through to "no handler" (LoginFrameDispatcher-style warning log).
/// </summary>
public sealed class HeartbeatHandler : IInlinePacketHandler<HeartbeatRequest>
{
    public void Handle(in HeartbeatRequest packet, IPacketSession session)
    {
        // Intentionally empty -- see class doc.
    }
}

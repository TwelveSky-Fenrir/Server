using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     Final client ACK for zone entry (op13, wire contract §5.6). No validation of tTribe == mDATA.aTribe here:
///     the legacy check exists, but ZoneClientSession does not carry the player's tribe and this opcode is
///     already gated to AllowedStates=[Registering], reachable only after a successful registration -- an
///     extra anti-tamper check was judged out of scope for M1 rather than added silently.
/// </summary>
public sealed class ZoneReadyHandler : IInlinePacketHandler<ZoneReadyRequest>
{
    public void Handle(in ZoneReadyRequest packet, IPacketSession session)
    {
        ((ZoneClientSession)session).MarkInWorld();
    }
}

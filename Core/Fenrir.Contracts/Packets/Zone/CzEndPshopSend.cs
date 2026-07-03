using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_END_PSHOP_SEND (CLIENT.h:311-314) — close a shop stall. <c>Sort</c> 1 = close local personal
///     shop, 2 = close proxy shop (routed to <c>mProxySystem.Process(..., 21)</c>).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.EndPshopSend, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CzEndPshopSend : IIncomingPacket<CzEndPshopSend>
{
    public required int Sort { get; init; }
}

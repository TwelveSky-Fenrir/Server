using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_END_PSHOP_RECV (ZONE.h:545-548) — reply to CZ_END_PSHOP_SEND, also emitted after a stall sells
///     out entirely. Unicast.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CloseShopStall, ExpectedSize = 5)]
public readonly partial record struct CloseShopStallResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}

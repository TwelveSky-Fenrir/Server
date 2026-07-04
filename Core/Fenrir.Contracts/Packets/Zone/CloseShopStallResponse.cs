using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Also emitted when a stall sells out entirely, not just on explicit close.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CloseShopStall, ExpectedSize = 5)]
public readonly partial record struct CloseShopStallResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}

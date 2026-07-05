using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Also emitted when a stall sells out entirely, not just on explicit close.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CloseShopStall, ExpectedSize = 5)]
public readonly partial record struct CloseShopStallResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}

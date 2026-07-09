using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// Also emitted when a stall sells out entirely, not just on explicit close.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CloseShopStall, ExpectedSize = 5)]
public readonly record struct CloseShopStallResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}

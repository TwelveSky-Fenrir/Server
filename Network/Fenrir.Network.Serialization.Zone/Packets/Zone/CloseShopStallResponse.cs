using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CloseShopStall, ExpectedSize = 5)]
public readonly partial record struct CloseShopStallResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}

using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CraftItem, ExpectedSize = 29)]
public readonly partial record struct CraftItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }
}

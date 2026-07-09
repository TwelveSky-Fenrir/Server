using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// The only forge response without a trailing Value[6] (9 bytes, not 33 like the other forge ZCs).
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CombineItem, ExpectedSize = 9)]
public readonly record struct CombineItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Cost { get; init; }
}

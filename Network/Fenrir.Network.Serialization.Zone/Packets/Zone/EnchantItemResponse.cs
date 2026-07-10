using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// Result: 0=success, 1=failure, 2=destroyed, 3=reset to +40, 4=protected, 8/9=scroll failure, 999=special.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.EnchantItem, ExpectedSize = 13)]
public readonly partial record struct EnchantItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Cost { get; init; }

    public required int Value { get; init; }
}

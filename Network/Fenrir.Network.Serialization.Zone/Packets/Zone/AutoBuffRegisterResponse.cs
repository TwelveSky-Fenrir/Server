using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AutoBuffRegister,
    ExpectedSize = 5)]
public readonly partial record struct AutoBuffRegisterResponse : IOutgoingPacket
{
    public required int Value { get; init; }
}

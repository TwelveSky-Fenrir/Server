using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AutoBuffActivation,
    ExpectedSize = 5)]
public readonly partial record struct AutoBuffActivationResponse : IOutgoingPacket
{
    public required int Value { get; init; }
}

using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.MountState, ExpectedSize = 9)]
public readonly partial record struct MountStateResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}

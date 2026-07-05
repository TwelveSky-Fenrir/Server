using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DuelEnd, ExpectedSize = 5)]
public readonly partial record struct DuelEndResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}

using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneGreeting, ExpectedSize = 5)]
public readonly partial record struct ZoneGreetingResponse : IOutgoingPacket
{
    public required int RandomNumber { get; init; }
}

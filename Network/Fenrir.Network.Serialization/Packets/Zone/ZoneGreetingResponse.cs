using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneGreeting, ExpectedSize = 5)]
public readonly partial record struct ZoneGreetingResponse : IOutgoingPacket
{
    public required int RandomNumber { get; init; }
}

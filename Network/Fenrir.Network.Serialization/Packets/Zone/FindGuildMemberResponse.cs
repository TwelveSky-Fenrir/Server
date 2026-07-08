using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Result is the zone number where the searched avatar was found.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FindGuildMember, ExpectedSize = 5)]
public readonly record struct FindGuildMemberResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}

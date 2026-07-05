using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Result is the zone number where the searched avatar was found.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FindGuildMember, ExpectedSize = 5)]
public readonly partial record struct FindGuildMemberResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}

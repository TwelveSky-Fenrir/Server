using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.MentorStatus, ExpectedSize = 5)]
public readonly partial record struct MentorStatusResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}

using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TeacherAnswerRecv, ExpectedSize = 5)]
public readonly partial record struct ZcTeacherAnswerRecv : IOutgoingPacket
{
    public required int Answer { get; init; }
}

using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DuelAnswerRecv, ExpectedSize = 5)]
public readonly partial record struct ZcDuelAnswerRecv : IOutgoingPacket
{
    public required int Answer { get; init; }
}

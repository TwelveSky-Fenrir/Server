using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// TS2_0 is off: the builder drops its tResult argument - wire has 5 ints, no result field.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.QuestProgress, ExpectedSize = 21)]
public readonly partial record struct QuestProgressResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Page { get; init; }
    public required int Index { get; init; }
    public required int XPost { get; init; }
    public required int YPost { get; init; }
}

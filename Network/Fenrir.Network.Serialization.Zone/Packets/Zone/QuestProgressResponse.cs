using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// TS2_0 is off: the builder drops its tResult argument - wire has 5 ints, no result field.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.QuestProgress, ExpectedSize = 21)]
public readonly record struct QuestProgressResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Page { get; init; }
    public required int Index { get; init; }
    public required int XPost { get; init; }
    public required int YPost { get; init; }
}

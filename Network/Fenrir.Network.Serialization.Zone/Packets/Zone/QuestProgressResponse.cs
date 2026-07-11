using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.QuestProgress, ExpectedSize = 21)]
public readonly partial record struct QuestProgressResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Page { get; init; }
    public required int Index { get; init; }
    public required int XPost { get; init; }
    public required int YPost { get; init; }
}

using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.QuestProgress, ExpectedSize = 21)]
public readonly partial record struct QuestProgressResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Page { get; init; }
    public required int Index { get; init; }
    public required int XPost { get; init; }
    public required int YPost { get; init; }
}

using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.FishingProgress,
    ExpectedSize = 21)]
public readonly partial record struct FishingProgressResponse : IOutgoingPacket
{
    public required int ServerIndex { get; init; }
    public required uint UniqueNumber { get; init; }
    public required int Result { get; init; }
    public required int FishingState { get; init; }
    public required int FishingStep { get; init; }
}

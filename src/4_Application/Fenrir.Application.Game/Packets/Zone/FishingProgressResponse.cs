using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

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

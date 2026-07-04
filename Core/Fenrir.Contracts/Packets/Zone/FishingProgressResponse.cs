using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Result = echo of the request's Sort (1/2/3), or 1 on the reward/timeout path. Also pushed server-side on line timeout.
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

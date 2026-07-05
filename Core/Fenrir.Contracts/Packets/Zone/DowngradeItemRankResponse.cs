using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DowngradeItemRank, ExpectedSize = 33)]
public readonly partial record struct DowngradeItemRankResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Cost { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }
}

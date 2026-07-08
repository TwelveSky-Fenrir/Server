using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Real reward is CP, not an item; every field but Result is zero (the multi-item path is dead code in EU33).</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.HeroRewardClaim, ExpectedSize = 57)]
public readonly record struct HeroRewardClaimResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Page { get; init; }
    public required int Index1 { get; init; }
    public required int Index2 { get; init; }
    public required int Xy1 { get; init; }
    public required int Xy2 { get; init; }
    [FixedArray(8)] public required int[] ItemIndex { get; init; }
}

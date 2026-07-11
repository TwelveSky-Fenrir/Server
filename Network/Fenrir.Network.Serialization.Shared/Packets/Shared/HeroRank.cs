using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(680)]
public readonly partial record struct HeroRank : IFenrirWireType<HeroRank>
{
    [FixedArray(40)] [FixedString(13)] public required string[] Name { get; init; }

    [FixedArray(40)] public required int[] Point { get; init; }
}

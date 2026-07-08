using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

[FenrirWireType(24)]
public readonly record struct ItemLinkInfo : IFenrirWireType<ItemLinkInfo>
{
    public required int Index { get; init; }

    public required int Activity { get; init; }

    public required int Value { get; init; }

    [FixedArray(3)] public required int[] Socket { get; init; }
}
